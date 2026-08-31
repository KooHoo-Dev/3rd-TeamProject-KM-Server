namespace HelloServer;

public enum SessionPhase
{
    WaitingForBoards,
    WaitingForRoll,
    WaitingForDiceAnimation,
    Ended
}

public class GameSession
{
    private const int MAX_ROUND_COUNT = 20;
    
    private readonly string[] memberIds;

    private readonly HashSet<string> readyMembers = new();
    private readonly HashSet<string> diceAnimationEndedMembers = new();
    
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

        diceAnimationEndedMembers.Clear();
        Phase = SessionPhase.WaitingForDiceAnimation;

        return new DiceRolledMessage
        {
            TurnId = turnId, 
            PlayerId = memberId, 
            D1 = d1, 
            D2 = d2
        };
    }
    
    public bool ReportDiceAnimationEnded(string memberId, int turnId)
    {
        if (Phase != SessionPhase.WaitingForDiceAnimation) return false;
        if (turnId != TurnId) return false;
        if (memberIds.Contains(memberId) == false) return false;
        if (diceAnimationEndedMembers.Add(memberId) == false) return false;
        if (diceAnimationEndedMembers.Count != memberIds.Length) return false;

        FinishTurn(); // 일단 지금은 턴 종료인데, 타일 처리로 나중에 바꿔야 함
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
        => new() { RoundCount = RoundCount, TurnId = TurnId, PlayerId = CurrentMemberId };
}
