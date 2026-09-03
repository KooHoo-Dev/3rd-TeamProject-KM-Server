namespace HelloServer;

#region CLIENT_TO_SERVER

public class SetGameReadyMessage
{
    public string Type { get; set; } = ProtocolHeader.SET_GAME_READY;
    public bool IsReady { get; set; }
}

public class StartGameMessage
{
    public string Type { get; set; } = ProtocolHeader.START_GAME;
}

#endregion

#region SERVER_TO_CLIENT

public class GameReadySetMessage
{
    public string Type { get; set; } = ProtocolHeader.GAME_READY_SET;
    public string Id { get; set; }
    public bool IsReady { get; set; }
}

public class GameStartedMessage
{
    public string Type { get; set; } = ProtocolHeader.GAME_STARTED;
    public string[] MemberIds { get; set; }
}

#endregion