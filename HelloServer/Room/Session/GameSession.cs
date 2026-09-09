namespace HelloServer;

public enum SessionPhase
{
    WaitingForBoards,
    WaitingForRoll,
    WaitingForTurnFinished,
    Ended
}

public sealed class MemberRemovalResult
{
    public bool Removed { get; init; }
    public bool ShouldBroadcastTurnStarted { get; init; }
    public bool GameEnded { get; init; }
    public string WinnerId { get; init; }
    public int[] ClearedTerritoryIds { get; init; } = Array.Empty<int>();
}

public class GameSession
{
    private class TerritoryState
    {
        public string OwnerId;
        public bool HasBuilding;
        public bool HasLandMark;
    }
    
    private const int MAX_ROUND_COUNT = 40;
    private const int INITIAL_GOLD = 5000000;
    private const int EQUIPMENT_SLOT_COUNT = 5;
    
    private readonly List<string> memberIds;

    private readonly HashSet<string> readyMembers = new();
    private readonly HashSet<string> turnFinishedMembers = new();

    private readonly Dictionary<string, int> memberGolds = new();
    private readonly Dictionary<string, int> memberIncapacitationCounts = new();
    private readonly Dictionary<string, EquipmentSlotState[]> memberInventories = new();

    private readonly Dictionary<int, TerritoryState> territoryStates = new();
    
    private int currentMemberIndex;
    
    public int RoundCount { get; private set; }
    public int TurnId { get; private set; }

    public SessionPhase Phase { get; private set; } = SessionPhase.WaitingForBoards;
    public int MemberCount => memberIds.Count;
    
    public string CurrentMemberId => memberIds[currentMemberIndex];

    public GameSession(string[] memberIds)
    {
        string[] shuffledMemberIds = (string[])memberIds.Clone();
        Random.Shared.Shuffle(shuffledMemberIds); // 일단 초기 구현에서는 랜덤 순서 배정으로 둠
        this.memberIds = shuffledMemberIds.ToList();

        // 서버에 초기 자금 업데이트
        for (int i = 0; i < memberIds.Length; i++)
        {
            string id = memberIds[i];
            memberGolds[id] = INITIAL_GOLD;
            memberIncapacitationCounts[id] = 0;
            memberInventories[id] = new EquipmentSlotState[EQUIPMENT_SLOT_COUNT];
        }
    }

    #region REPORT_AND_REQUEST

    public bool ReportBoardReady(string memberId)
    {
        if (Phase != SessionPhase.WaitingForBoards) return false;
        if (memberIds.Contains(memberId) == false) return false;
        if (readyMembers.Add(memberId) == false) return false;
        if (readyMembers.Count != memberIds.Count) return false;

        currentMemberIndex = 0;
        
        RoundCount = 1;
        TurnId = 1;

        Phase = SessionPhase.WaitingForRoll;
        
        return true;
    }

    public DiceRolledMessage TryRollDice(string memberId, int turnId)
    {
        if (Phase != SessionPhase.WaitingForRoll) return null;
        if (turnId != TurnId) return null;
        if (memberId != CurrentMemberId) return null;

        int d1 = Random.Shared.Next(1, 7);
        int d2 = Random.Shared.Next(1, 7);
        
        Phase = SessionPhase.WaitingForTurnFinished;

        return new DiceRolledMessage
        {
            TurnId = turnId, 
            PlayerId = memberId, 
            D1 = d1, 
            D2 = d2
        };
    }

    public bool TryBeginTurnWithoutRoll(string memberId, int turnId)
    {
        if (Phase != SessionPhase.WaitingForRoll) return false;
        if (turnId != TurnId) return false;
        if (memberId != CurrentMemberId) return false;

        Phase = SessionPhase.WaitingForTurnFinished;
        return true;
    }

    public GoldCardDrawnMessage TryDrawGoldCard(string memberId, int turnId, int[] cardIds)
    {
        if (Phase != SessionPhase.WaitingForTurnFinished) return null;
        if (turnId != TurnId) return null;
        if (memberId != CurrentMemberId) return null;
        
        int randIdx = Random.Shared.Next(0, cardIds.Length);
        int cardId = cardIds[randIdx];

        return new GoldCardDrawnMessage
        {
            TurnId = turnId, 
            CardId = cardId,
            PlayerId = memberId
        };
    }
    
    public bool ReportTurnFinished(string memberId, int turnId)
    {
        if (Phase != SessionPhase.WaitingForTurnFinished) return false;
        if (turnId != TurnId) return false;
        if (memberIds.Contains(memberId) == false) return false;
        if (turnFinishedMembers.Add(memberId) == false) return false;
        if (turnFinishedMembers.Count != memberIds.Count) return false;
        
        AdvanceTurn();
        return true;
    }

    public MemberRemovalResult RemoveMember(string memberId)
    {
        int removedIndex = memberIds.IndexOf(memberId);
        if (removedIndex < 0) return new MemberRemovalResult();

        SessionPhase previousPhase = Phase;
        bool removedCurrentMember = previousPhase is not (SessionPhase.WaitingForBoards or SessionPhase.Ended) &&
                                    removedIndex == currentMemberIndex;
        bool removedLastMemberInOrder = removedIndex == memberIds.Count - 1;

        memberIds.RemoveAt(removedIndex);
        readyMembers.Remove(memberId);
        turnFinishedMembers.Remove(memberId);
        memberGolds.Remove(memberId);
        memberIncapacitationCounts.Remove(memberId);
        memberInventories.Remove(memberId);

        List<int> clearedTerritoryIds = new();
        foreach ((int tileId, TerritoryState state) in territoryStates)
        {
            if (state.OwnerId != memberId) continue;

            state.OwnerId = string.Empty;
            state.HasBuilding = false;
            state.HasLandMark = false;
            clearedTerritoryIds.Add(tileId);
        }

        if (memberIds.Count <= 1)
        {
            Phase = SessionPhase.Ended;
            currentMemberIndex = 0;

            return new MemberRemovalResult
            {
                Removed = true,
                GameEnded = previousPhase != SessionPhase.Ended,
                WinnerId = memberIds.Count == 1 ? memberIds[0] : null,
                ClearedTerritoryIds = clearedTerritoryIds.ToArray()
            };
        }

        bool shouldBroadcastTurnStarted = false;

        if (previousPhase == SessionPhase.WaitingForBoards)
        {
            if (readyMembers.Count == memberIds.Count)
            {
                StartFirstTurn();
                shouldBroadcastTurnStarted = true;
            }
        }
        else if (previousPhase != SessionPhase.Ended)
        {
            if (removedIndex < currentMemberIndex)
                currentMemberIndex--;
            else if (currentMemberIndex >= memberIds.Count)
                currentMemberIndex = 0;

            if (removedCurrentMember)
            {
                turnFinishedMembers.Clear();

                if (removedLastMemberInOrder && RoundCount >= MAX_ROUND_COUNT)
                {
                    Phase = SessionPhase.Ended;
                }
                else
                {
                    if (removedLastMemberInOrder)
                        RoundCount++;

                    TurnId++;
                    Phase = SessionPhase.WaitingForRoll;
                    shouldBroadcastTurnStarted = true;
                }
            }
            else if (previousPhase == SessionPhase.WaitingForTurnFinished &&
                     turnFinishedMembers.Count == memberIds.Count)
            {
                AdvanceTurn();
                shouldBroadcastTurnStarted = Phase != SessionPhase.Ended;
            }
        }

        return new MemberRemovalResult
        {
            Removed = true,
            ShouldBroadcastTurnStarted = shouldBroadcastTurnStarted,
            GameEnded = previousPhase != SessionPhase.Ended && Phase == SessionPhase.Ended,
            ClearedTerritoryIds = clearedTerritoryIds.ToArray()
        };
    }

    private void StartFirstTurn()
    {
        currentMemberIndex = 0;
        RoundCount = 1;
        TurnId = 1;
        Phase = SessionPhase.WaitingForRoll;
    }

    private void AdvanceTurn()
    {
        bool isLastMember = currentMemberIndex == memberIds.Count - 1;

        if (isLastMember && RoundCount >= MAX_ROUND_COUNT)
        {
            Phase = SessionPhase.Ended;
            return;
        }

        currentMemberIndex++;

        if (currentMemberIndex >= memberIds.Count)
        {
            currentMemberIndex = 0;
            RoundCount++;
        }

        TurnId++;
        Phase = SessionPhase.WaitingForRoll;
    }
    
    #endregion
    
    public bool TryChangeGold(string memberId, int amount)
    {
        if (memberGolds.ContainsKey(memberId) == false)
        {
            Console.WriteLine($"Member {memberId} does not exist.");
            return false;
        }
        
        memberGolds[memberId] += amount;
        return true;
    }

    public bool CanDeclareBankruptcy(string memberId)
    {
        if (memberGolds.TryGetValue(memberId, out int gold) == false || gold > 0)
            return false;

        return territoryStates.Values.All(state => state.OwnerId != memberId);
    }
    
    public void AddIncapacitationCount(string memberId, int count)
    {
        if (count < 0) return;
        memberIncapacitationCounts[memberId] += count;
    }

    public void UpdateTerritoryState(int tileId, string ownerId, bool hasBuilding, bool hasLandMark)
    {
        TerritoryState state = new TerritoryState
        {
            OwnerId = ownerId,
            HasBuilding = hasBuilding,
            HasLandMark = hasLandMark
        };

        territoryStates[tileId] = state;
    }

    public bool IsTerritoryOwnedBy(int tileId, string ownerId)
    {
        return territoryStates.TryGetValue(tileId, out TerritoryState state) &&
               state.OwnerId == ownerId;
    }

    public bool TryChangeTerritoryOwner(int tileId, string ownerId)
    {
        if (territoryStates.TryGetValue(tileId, out TerritoryState state) == false) return false;

        state.OwnerId = ownerId;
        return true;
    }

    public bool TrySetEquipment(string memberId, SetEquipmentMessage message)
    {
        if (memberInventories.TryGetValue(memberId, out EquipmentSlotState[] slots) == false) return false;
        if (message.InstanceId <= 0 || message.EquipmentId <= 0) return false;
        if (message.SlotIndex < 0 || message.SlotIndex >= EQUIPMENT_SLOT_COUNT) return false;

        for (int i = 0; i < slots.Length; i++)
        {
            if (i != message.SlotIndex && slots[i]?.InstanceId == message.InstanceId)
                return false;
        }

        slots[message.SlotIndex] = new EquipmentSlotState
        {
            InstanceId = message.InstanceId,
            EquipmentId = message.EquipmentId,
            SlotIndex = message.SlotIndex
        };

        return true;
    }

    public bool TryRemoveEquipment(string memberId, RemoveEquipmentMessage message)
    {
        if (memberInventories.TryGetValue(memberId, out EquipmentSlotState[] slots) == false) return false;
        if (message.SlotIndex < 0 || message.SlotIndex >= EQUIPMENT_SLOT_COUNT) return false;
        if (slots[message.SlotIndex]?.InstanceId != message.InstanceId) return false;

        for (int i = message.SlotIndex; i < slots.Length - 1; i++)
        {
            slots[i] = slots[i + 1];

            if (slots[i] != null)
                slots[i].SlotIndex = i;
        }

        slots[slots.Length - 1] = null;
        return true;
    }

    #region CREATE_MESSAGE
    
    public TurnStartedMessage CreateTurnStartedMessage()
    {
        turnFinishedMembers.Clear();
        
        int actionDisableCount = memberIncapacitationCounts[CurrentMemberId];
        bool canAct = actionDisableCount == 0;

        if (canAct == false)
        {
            memberIncapacitationCounts[CurrentMemberId]--;
            
            turnFinishedMembers.Clear();
            Phase = SessionPhase.WaitingForTurnFinished;
        }
        
        return new TurnStartedMessage
        {
            RoundCount = RoundCount,
            TurnId = TurnId,
            
            PlayerId = CurrentMemberId,
            CanAct = canAct
        };
    }

    public EconomyUpdatedMessage CreateEconomyUpdatedMessage()
    {
        return new EconomyUpdatedMessage
        {
            Economies = memberIds
                .Select(id => new UserEconomy
                {
                    UserId = id, 
                    Gold = memberGolds[id]
                })
                .ToArray()
        };
    }

    public UpdateTerritoryMessage CreateTerritoryUpdatedMessage(int tileId)
    {
        TerritoryState state = territoryStates[tileId];

        return new UpdateTerritoryMessage
        {
            TileId = tileId, 
            OwnerId = state.OwnerId, 
            HasBuilding = state.HasBuilding, 
            HasLandMark = state.HasLandMark
        };
    }

    public InventoryUpdatedMessage CreateInventoryUpdatedMessage(string memberId)
    {
        return new InventoryUpdatedMessage
        {
            UserId = memberId,
            Equipments = memberInventories[memberId]
                .Where(equipment => equipment != null)
                .ToArray()
        };
    }
    
    #endregion
}
