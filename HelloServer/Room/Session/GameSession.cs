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
    private const int MAX_ROUND_COUNT = 20;
    
    private readonly string[] memberIds;

    private readonly HashSet<string> readyMembers = new();
    private readonly HashSet<string> turnFinishedMembers = new();
    
    private int currentMemberIndex;
    
    public int RoundCount { get; private set; }
    public int TurnId { get; private set; }

    public SessionPhase Phase { get; private set; } = SessionPhase.WaitingForBoards;
    
    public string CurrentMemberId => memberIds[currentMemberIndex];

    public GameSession(string[] memberIds)
    {
        this.memberIds = (string[])memberIds.Clone();
        Random.Shared.Shuffle(this.memberIds); // 일단 초기 구현에서는 랜덤 순서 배정으로 둠
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

        turnFinishedMembers.Clear();
        Phase = SessionPhase.WaitingForTurnFinished;

        return new DiceRolledMessage
        {
            TurnId = turnId, 
            PlayerId = memberId, 
            D1 = d1, 
            D2 = d2
        };
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

        FinishTurn();
        return true;
    }
    
    #endregion

    private void FinishTurn()
    {
        bool isLastMember = currentMemberIndex == memberIds.Length - 1;

        if (isLastMember && RoundCount >= MAX_ROUND_COUNT)
        {
            Phase = SessionPhase.Ended;
            return;
        }

        currentMemberIndex++;

        if (currentMemberIndex >= memberIds.Length)
        {
            currentMemberIndex = 0;
            RoundCount++;
        }

        TurnId++;
        Phase = SessionPhase.WaitingForRoll;
    }
    
    public TurnStartedMessage CreateTurnStartedMessage()
    {
        return new TurnStartedMessage
        {
            RoundCount = RoundCount,
            TurnId = TurnId,
            PlayerId = CurrentMemberId
        };
    }
}
