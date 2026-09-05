namespace HelloServer;

public partial class Room
{
    private class PendingUserMove
    {
        public long MoveId;
        public int TurnId;
        public string RequestId;
        public string UserId;

        public HashSet<string> WaitingMemberIds = new();
    }
    
    private GameSession session;
    private bool isGameStarted = false;

    private long lastMoveId;
    private readonly Dictionary<long, PendingUserMove> pendingUserMoves = new();
    private readonly Dictionary<string, HashSet<string>> tileEffectReadyMembers = new();

    private void RegisterGameHandlers()
    {
        RegisterGameHandler<SetBoardReadyMessage>(ProtocolHeader.SET_BOARD_READY, HandleSetBoardReadyAsync);
        RegisterGameHandler<RollDiceMessage>(ProtocolHeader.ROLL_DICE, HandleRollDiceAsync);
        RegisterGameHandler<TileEffectSyncMessage>(ProtocolHeader.TILE_EFFECT_SYNC, HandleTileEffectSyncAsync);
        RegisterGameHandler<TurnFinishedMessage>(ProtocolHeader.TURN_FINISHED, HandleTurnFinishedAsync);
        
        RegisterGameHandler<DrawGoldCardMessage>(ProtocolHeader.DRAW_GOLD_CARD, HandleDrawGoldCardAsync);
        RegisterGameHandler<UpdateEconomyMessage>(ProtocolHeader.UPDATE_ECONOMY, HandleUpdateEconomyAsync);
        RegisterGameHandler<MoveUserToMessage>(ProtocolHeader.MOVE_USER_TO, HandleUserMovedToAsync);
        RegisterGameHandler<UserMoveFinishedMessage>(ProtocolHeader.USER_MOVE_FINISHED, HandleUserMoveFinishedAsync);
        RegisterGameHandler<AddIncapacitationCountMessage>(ProtocolHeader.ADD_INCAPACITATION_COUNT, HandleAddIncapacitationCountAsync);
        RegisterGameHandler<UpdateTerritoryMessage>(ProtocolHeader.UPDATE_TERRITORY, HandleUpdateTerritoryAsync);
    }

    private void RegisterGameHandler<T>(string type, Func<Member, T, Task> handler)
    {
        RegisterHandler<T>(type, async (members, message) =>
        {
            await gate.WaitAsync();

            try
            {
                if (session == null) return;
                if (message == null) return;
                await handler(members, message);
            }
            finally
            {
                gate.Release();
            }
        });
    }

    #region HANDLE_RECEIVED_MESSAGE

    private async Task HandleSetBoardReadyAsync(Member member, SetBoardReadyMessage _)
    {
        if (session.ReportBoardReady(member.User.Id) == false)
            return;

        await BroadcastAsync(session.CreateEconomyUpdatedMessage());
        await BroadcastAsync(session.CreateTurnStartedMessage());
    }
    
    private async Task HandleRollDiceAsync(Member member, RollDiceMessage msg)
    {
        DiceRolledMessage result = 
            session.TryRollDice(member.User.Id, msg.TurnId);

        if (result != null) await BroadcastAsync(result);
    }

    private async Task HandleTileEffectSyncAsync(Member member, TileEffectSyncMessage msg)
    {
        if (session.Phase != SessionPhase.WaitingForTurnFinished) return;
        if (msg.TurnId != session.TurnId) return;
        if (msg.MoveId < 0) return;
        if (msg.StepIndex < 0) return;

        PendingUserMove pendingUserMove = null;

        if (msg.MoveId > 0)
        {
            if (pendingUserMoves.TryGetValue(msg.MoveId, out pendingUserMove) == false)
                return;

            if (pendingUserMove.TurnId != msg.TurnId) return;
        }

        if (msg.Phase == TileEffectSyncPhase.Ready)
        {
            await HandleTileEffectReadyAsync(member, msg);
            return;
        }

        if (msg.Phase != TileEffectSyncPhase.Resolved) return;

        if (msg.MoveId == 0)
        {
            if (member.User.Id != session.CurrentMemberId) return;
        }
        else if (pendingUserMove.UserId != member.User.Id) return;

        await BroadcastAsync(msg);
    }

    private async Task HandleTileEffectReadyAsync(Member member, TileEffectSyncMessage msg)
    {
        string key = CreateTileEffectKey(msg);

        if (tileEffectReadyMembers.TryGetValue(key, out HashSet<string> waitingMembers) == false)
        {
            waitingMembers = members.Keys.ToHashSet();
            tileEffectReadyMembers.Add(key, waitingMembers);
        }

        if (waitingMembers.Remove(member.User.Id) == false) return;
        if (waitingMembers.Count > 0) return;

        tileEffectReadyMembers.Remove(key);
        await BroadcastAsync(msg);
    }

    private static string CreateTileEffectKey(TileEffectSyncMessage msg)
        => $"{msg.TurnId}:{msg.MoveId}:{msg.StepIndex}";

    private async Task HandleDrawGoldCardAsync(Member member, DrawGoldCardMessage msg)
    {
        GoldCardDrawnMessage result = 
            session.TryDrawGoldCard(member.User.Id, msg.TurnId, msg.CardIds);
        
        if (result != null) await BroadcastAsync(result);
    }

    private async Task HandleTurnFinishedAsync(Member member, TurnFinishedMessage msg)
    {
        if (pendingUserMoves.Count > 0) return;
        
        bool isAdvanced =
            session.ReportTurnFinished(member.User.Id, msg.TurnId);

        if (isAdvanced == false) return;
        
        object message = session.Phase == SessionPhase.Ended ? 
            new GameEndedMessage() : 
            session.CreateTurnStartedMessage();
        
        await BroadcastAsync(message);
    }

    private async Task HandleUpdateEconomyAsync(Member _, UpdateEconomyMessage msg)
    {
        for (int i = 0; i < msg.Updates.Length; i++)
        {
            EconomyUpdate update = msg.Updates[i];
            session.TryChangeGold(update.UserId, update.Amount);
        }

        EconomyUpdatedMessage result = session.CreateEconomyUpdatedMessage();
        await BroadcastAsync(result);
    }

    private Task HandleAddIncapacitationCountAsync(Member member, AddIncapacitationCountMessage msg)
    {
        session.AddIncapacitationCount(member.User.Id, msg.Count);
        return Task.CompletedTask;
    }

    private async Task HandleUpdateTerritoryAsync(Member member, UpdateTerritoryMessage msg)
    {
        int tileId = msg.TileId;
        
        session.UpdateTerritoryState(
            tileId, 
            msg.OwnerId, 
            msg.HasBuilding, 
            msg.HasLandMark);
        
        UpdateTerritoryMessage result = session.CreateTerritoryUpdatedMessage(tileId);
        await BroadcastAsync(result);
    }

    private async Task HandleUserMovedToAsync(Member member, MoveUserToMessage msg)
    {
        if (session.Phase != SessionPhase.WaitingForTurnFinished) return;
        if (msg.TurnId != session.TurnId) return;
        if (member.User.Id != session.CurrentMemberId) return;
        if (members.ContainsKey(msg.UserId) == false) return;

        long moveId = Interlocked.Increment(ref lastMoveId);

        pendingUserMoves[moveId] = new PendingUserMove
        {
            MoveId = moveId, 
            TurnId = msg.TurnId, 
            RequestId = msg.RequestId, 
            UserId = msg.UserId,
            WaitingMemberIds = members.Keys.ToHashSet()
        };

        MoveUserToMessage result = new MoveUserToMessage
        {
            TurnId = msg.TurnId,
            MoveId = moveId,
            UserId = msg.UserId,
            TileId = msg.TileId
        };
        
        await BroadcastAsync(result);
    }

    private async Task HandleUserMoveFinishedAsync(Member member, UserMoveFinishedMessage msg)
    {
        if (pendingUserMoves.TryGetValue(msg.MoveId, out PendingUserMove pendingUserMove) == false) return;
        if (pendingUserMove.TurnId != msg.TurnId) return;
        if (pendingUserMove.WaitingMemberIds.Remove(member.User.Id) == false) return;
        if (pendingUserMove.WaitingMemberIds.Count > 0) return;

        UserMoveFinishedMessage result = new()
        {
            TurnId = pendingUserMove.TurnId, 
            MoveId = pendingUserMove.MoveId, 
            RequestId = pendingUserMove.RequestId
        };

        pendingUserMoves.Remove(pendingUserMove.MoveId);
        
        await BroadcastAsync(result);
    }

    #endregion
}
