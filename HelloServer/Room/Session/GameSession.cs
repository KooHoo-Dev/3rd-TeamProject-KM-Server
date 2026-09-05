namespace HelloServer;

public enum SessionPhase
{
    WaitingForBoards,
    WaitingForRoll,
    WaitingForTurnFinished,
    Ended
}

public class GameSession
{
    private class TerritoryState
    {
        public string OwnerId;
        public bool HasBuilding;
        public bool HasLandMark;
    }
    
    private const int MAX_ROUND_COUNT = 20;
    private const int INITIAL_GOLD = 100000;
    
    private readonly string[] memberIds;

    private readonly HashSet<string> readyMembers = new();
    private readonly HashSet<string> turnFinishedMembers = new();

    private readonly Dictionary<string, int> memberGolds = new();
    private readonly Dictionary<string, int> memberIncapacitationCounts = new();

    private readonly Dictionary<int, TerritoryState> territoryStates = new();
    
    private int currentMemberIndex;
    
    public int RoundCount { get; private set; }
    public int TurnId { get; private set; }

    public SessionPhase Phase { get; private set; } = SessionPhase.WaitingForBoards;
    
    public string CurrentMemberId => memberIds[currentMemberIndex];

    public GameSession(string[] memberIds)
    {
        this.memberIds = (string[])memberIds.Clone();
        Random.Shared.Shuffle(this.memberIds); // 일단 초기 구현에서는 랜덤 순서 배정으로 둠

        // 서버에 초기 자금 업데이트
        for (int i = 0; i < memberIds.Length; i++)
        {
            string id = memberIds[i];
            memberGolds[id] = INITIAL_GOLD;
            memberIncapacitationCounts[id] = 0;
        }
    }

    #region REPORT_AND_REQUEST

    public bool ReportBoardReady(string memberId)
    {
        if (Phase != SessionPhase.WaitingForBoards) return false;
        if (memberIds.Contains(memberId) == false) return false;
        if (readyMembers.Add(memberId) == false) return false;
        if (readyMembers.Count != memberIds.Length) return false;

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
        if (turnFinishedMembers.Count != memberIds.Length) return false;
        
        bool isLastMember = currentMemberIndex == memberIds.Length - 1;

        if (isLastMember && RoundCount >= MAX_ROUND_COUNT)
        {
            Phase = SessionPhase.Ended;
            return true;
        }

        currentMemberIndex++;

        if (currentMemberIndex >= memberIds.Length)
        {
            currentMemberIndex = 0;
            RoundCount++;
        }

        TurnId++;
        Phase = SessionPhase.WaitingForRoll;
        
        return true;
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
    
    #endregion
}
