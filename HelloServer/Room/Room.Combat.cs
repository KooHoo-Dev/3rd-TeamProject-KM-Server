namespace HelloServer;

public partial class Room
{
    private const int COMBAT_START_HP = 50;
    private const int COMBAT_DURATION_SECONDS = 30;

    private enum CombatState
    {
        Offered,
        Preparing,
        Fighting
    }

    private sealed class ActiveCombat
    {
        public string CombatId;
        public string RequestId;
        public int TurnId;
        public int TileId;
        public string AttackerId;
        public string DefenderId;
        public int AttackerHp = COMBAT_START_HP;
        public int DefenderHp = COMBAT_START_HP;
        public CombatState State = CombatState.Offered;
        public HashSet<string> ReadyMemberIds = new();
    }

    private sealed class CombatDepartureResult
    {
        public string WinnerId;
        public string AttackerId;
        public int TileId;
    }

    private ActiveCombat activeCombat;

    private void RegisterCombatHandlers()
    {
        RegisterGameHandler<CombatRequestMessage>(
            ProtocolHeader.COMBAT_REQUEST,
            HandleCombatRequestAsync);
        RegisterGameHandler<CombatResponseMessage>(
            ProtocolHeader.COMBAT_RESPONSE,
            HandleCombatResponseAsync);
        RegisterGameHandler<CombatReadyMessage>(
            ProtocolHeader.COMBAT_READY,
            HandleCombatReadyAsync);
        RegisterGameHandler<CombatPositionMessage>(
            ProtocolHeader.COMBAT_POSITION,
            HandleCombatPositionAsync);
        RegisterGameHandler<SkillCastMessage>(
            ProtocolHeader.SKILL_CAST,
            HandleSkillCastAsync);
        RegisterGameHandler<CombatHealthReportMessage>(
            ProtocolHeader.COMBAT_HEALTH_REPORT,
            HandleCombatHealthReportAsync);
    }

    private async Task HandleCombatRequestAsync(Member member, CombatRequestMessage message)
    {
        string attackerId = member.User.Id;

        bool isValid =
            activeCombat == null &&
            session.Phase == SessionPhase.WaitingForTurnFinished &&
            session.CurrentMemberId == attackerId &&
            message.TurnId == session.TurnId &&
            !string.IsNullOrWhiteSpace(message.RequestId) &&
            message.TileId >= 0 &&
            message.DefenderId != attackerId &&
            members.ContainsKey(message.DefenderId) &&
            session.IsTerritoryOwnedBy(message.TileId, message.DefenderId);

        if (!isValid)
        {
            await SendAsync(member, new CombatCancelledMessage
            {
                RequestId = message.RequestId,
                Reason = "invalidRequest"
            });
            return;
        }

        activeCombat = new ActiveCombat
        {
            CombatId = Guid.NewGuid().ToString("N"),
            RequestId = message.RequestId,
            TurnId = message.TurnId,
            TileId = message.TileId,
            AttackerId = attackerId,
            DefenderId = message.DefenderId
        };

        Member defender = members[message.DefenderId];
        await SendAsync(defender, new CombatOfferMessage
        {
            CombatId = activeCombat.CombatId,
            RequestId = activeCombat.RequestId,
            TurnId = activeCombat.TurnId,
            TileId = activeCombat.TileId,
            AttackerId = activeCombat.AttackerId,
            DefenderId = activeCombat.DefenderId
        });
    }

    private async Task HandleCombatResponseAsync(Member member, CombatResponseMessage message)
    {
        if (activeCombat == null || activeCombat.State != CombatState.Offered) return;
        if (message.CombatId != activeCombat.CombatId) return;
        if (member.User.Id != activeCombat.DefenderId) return;

        bool rejectSucceeded = !message.Accepted && Random.Shared.Next(2) == 0;

        if (rejectSucceeded)
        {
            ActiveCombat cancelled = activeCombat;
            activeCombat = null;

            await SendToCombatantsAsync(cancelled, new CombatCancelledMessage
            {
                CombatId = cancelled.CombatId,
                RequestId = cancelled.RequestId,
                Reason = "defenderRejected"
            });
            return;
        }

        activeCombat.State = CombatState.Preparing;

        await SendToCombatantsAsync(activeCombat, new CombatAssignmentMessage
        {
            CombatId = activeCombat.CombatId,
            RequestId = activeCombat.RequestId,
            AttackerId = activeCombat.AttackerId,
            DefenderId = activeCombat.DefenderId,
            StartHp = COMBAT_START_HP,
            DurationSeconds = COMBAT_DURATION_SECONDS
        });
    }

    private async Task HandleCombatReadyAsync(Member member, CombatReadyMessage message)
    {
        if (activeCombat == null || activeCombat.State != CombatState.Preparing) return;
        if (message.CombatId != activeCombat.CombatId) return;
        if (!IsCombatant(activeCombat, member.User.Id)) return;
        if (!activeCombat.ReadyMemberIds.Add(member.User.Id)) return;
        if (activeCombat.ReadyMemberIds.Count < 2) return;

        activeCombat.State = CombatState.Fighting;

        await SendToCombatantsAsync(activeCombat, new CombatStartedMessage
        {
            CombatId = activeCombat.CombatId
        });

        _ = RunCombatTimeoutAsync(activeCombat.CombatId);
    }

    private async Task HandleCombatPositionAsync(Member member, CombatPositionMessage message)
    {
        if (activeCombat == null || activeCombat.State != CombatState.Fighting) return;
        if (message.CombatId != activeCombat.CombatId) return;
        if (!IsCombatant(activeCombat, member.User.Id)) return;
        if (!float.IsFinite(message.X)) return;

        message.PlayerId = member.User.Id;
        await SendToCombatantsAsync(activeCombat, message);
    }

    private async Task HandleSkillCastAsync(Member member, SkillCastMessage message)
    {
        if (activeCombat == null || activeCombat.State != CombatState.Fighting) return;
        if (message.CombatId != activeCombat.CombatId) return;
        if (!IsCombatant(activeCombat, member.User.Id)) return;
        if (string.IsNullOrWhiteSpace(message.CastId)) return;

        message.CasterId = member.User.Id;
        await SendToCombatantsAsync(activeCombat, message);
    }

    private async Task HandleCombatHealthReportAsync(
        Member member,
        CombatHealthReportMessage message)
    {
        if (activeCombat == null || activeCombat.State != CombatState.Fighting) return;
        if (message.CombatId != activeCombat.CombatId) return;

        string playerId = member.User.Id;
        if (!IsCombatant(activeCombat, playerId)) return;

        int previousHp = playerId == activeCombat.AttackerId
            ? activeCombat.AttackerHp
            : activeCombat.DefenderHp;

        if (message.RemainingHp < 0 || message.RemainingHp > previousHp) return;

        if (playerId == activeCombat.AttackerId)
            activeCombat.AttackerHp = message.RemainingHp;
        else
            activeCombat.DefenderHp = message.RemainingHp;

        await SendToCombatantsAsync(activeCombat, new CombatHealthUpdatedMessage
        {
            CombatId = activeCombat.CombatId,
            PlayerId = playerId,
            RemainingHp = message.RemainingHp
        });

        if (message.RemainingHp > 0) return;

        await FinishCombatAsync(activeCombat, playerId);
    }

    private async Task RunCombatTimeoutAsync(string combatId)
    {
        await Task.Delay(TimeSpan.FromSeconds(COMBAT_DURATION_SECONDS));
        await gate.WaitAsync();

        try
        {
            if (activeCombat == null || activeCombat.CombatId != combatId) return;
            if (activeCombat.State != CombatState.Fighting) return;

            string loserId = activeCombat.AttackerHp > activeCombat.DefenderHp
                ? activeCombat.DefenderId
                : activeCombat.AttackerId;

            await FinishCombatAsync(activeCombat, loserId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CombatDepartureResult> ResolveCombatDepartureAsync(string memberId)
    {
        if (activeCombat == null || !IsCombatant(activeCombat, memberId)) return null;

        ActiveCombat combat = activeCombat;

        if (combat.State == CombatState.Offered)
        {
            activeCombat = null;
            await SendToCombatantsAsync(combat, new CombatCancelledMessage
            {
                CombatId = combat.CombatId,
                RequestId = combat.RequestId,
                Reason = "A combatant left the room."
            });

            return null;
        }

        string winnerId = memberId == combat.AttackerId
            ? combat.DefenderId
            : combat.AttackerId;

        await FinishCombatAsync(combat, memberId);

        return new CombatDepartureResult
        {
            WinnerId = winnerId,
            AttackerId = combat.AttackerId,
            TileId = combat.TileId
        };
    }

    private async Task FinishCombatAsync(ActiveCombat combat, string loserId)
    {
        if (activeCombat != combat) return;

        string winnerId = loserId == combat.AttackerId
            ? combat.DefenderId
            : combat.AttackerId;

        activeCombat = null;

        await SendToCombatantsAsync(combat, new CombatResultMessage
        {
            CombatId = combat.CombatId,
            WinnerId = winnerId,
            LoserId = loserId
        });
    }

    private async Task SendToCombatantsAsync(ActiveCombat combat, object message)
    {
        List<Task> sends = new();

        if (members.TryGetValue(combat.AttackerId, out Member attacker))
            sends.Add(SendAsync(attacker, message));

        if (members.TryGetValue(combat.DefenderId, out Member defender))
            sends.Add(SendAsync(defender, message));

        await Task.WhenAll(sends);
    }

    private static bool IsCombatant(ActiveCombat combat, string memberId)
    {
        return memberId == combat.AttackerId || memberId == combat.DefenderId;
    }
}
