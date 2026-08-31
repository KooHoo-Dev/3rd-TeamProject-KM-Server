namespace HelloServer;

public class GameSession
{
    public string[] MemberIds { get; }

    public GameSession(string[] memberIds)
    {
        MemberIds = memberIds;
    }
}