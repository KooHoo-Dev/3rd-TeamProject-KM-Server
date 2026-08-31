namespace HelloServer;

public enum SessionPhase
{
    WaitingForBoards,
    WaitingForRoll,
    WaitingForPresentation,
    Ended
}

public class GameSession
{
    private const int MaxRoundCount = 20;

    private readonly HashSet<string> readyMembers = new();
    private readonly HashSet<string> presentedMembers = new();
    
    public string[] MemberIds { get; }

    public int RoundCount { get; private set; }
    public int CurrentMemberIndex { get; private set; }
    public int TurnId { get; private set; }

    public SessionPhase Phase { get; private set; } = SessionPhase.WaitingForBoards;
    
    public string CurrentMemberId => MemberIds[CurrentMemberIndex];

    public GameSession(string[] memberIds)
    {
        MemberIds = memberIds;
        Random.Shared.Shuffle(MemberIds); // 일단 초기 구현에서는 랜덤 순서 배정으로 둠
    }

    public bool TryStart(string memberId)
    {
        if (Phase != SessionPhase.WaitingForBoards) return false;
        if (MemberIds.Contains(memberId) == false) return false;
        if (readyMembers.Add(memberId) == false) return false;
        if (readyMembers.Count != MemberIds.Length) return false;

        RoundCount = 1;
        CurrentMemberIndex = 0;
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

        presentedMembers.Clear();
        Phase = SessionPhase.WaitingForPresentation;

        return new DiceRolledMessage { TurnId = turnId, PlayerId = memberId, D1 = d1, D2 = d2 };
    }

    public bool TryCompletePresentation(string memberId, int turnId)
    {
        if (Phase != SessionPhase.WaitingForPresentation) return false;
        if (turnId != TurnId) return false;
        if (MemberIds.Contains(memberId) == false) return false;
        if (presentedMembers.Add(memberId) == false) return false;
        if (presentedMembers.Count != MemberIds.Length) return false;

        bool isLastMember = CurrentMemberIndex == MemberIds.Length - 1;

        if (isLastMember && RoundCount >= MaxRoundCount)
        {
            Phase = SessionPhase.Ended;
            return true;
        }

        CurrentMemberIndex++;

        if (CurrentMemberIndex >= MemberIds.Length)
        {
            CurrentMemberIndex = 0;
            RoundCount++;
        }

        TurnId++;
        Phase = SessionPhase.WaitingForRoll;

        return true;
    }
    
    public TurnStartedMessage CreateTurnStartedMessage() 
        => new() { RoundCount = RoundCount, TurnId = TurnId, PlayerId = CurrentMemberId };
}
