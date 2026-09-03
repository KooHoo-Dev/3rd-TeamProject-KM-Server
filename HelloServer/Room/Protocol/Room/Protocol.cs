namespace HelloServer;

public class User
{
    public string Id { get; set; }
    public string Nickname { get; set; }

    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
}

public class MessageHeader
{
    public string Type { get; set; }
}

#region CLIENT_TO_SERVER

public class HelloMessage
{
    public string Type { get; set; } = ProtocolHeader.HELLO;
    public string Nickname { get; set; }
}

#endregion

#region SERVER_TO_CLIENT

public class WelcomeMessage
{
    public string Type { get; set; } = ProtocolHeader.WELCOME;

    public string RoomCode { get; set; }

    public User User { get; set; }
    public User[] Users { get; set; }
}

public class JoinMessage
{
    public string Type { get; set; } = ProtocolHeader.JOIN;
    public User User { get; set; }
}

public class LeaveMessage
{
    public string Type { get; set; } = ProtocolHeader.LEAVE;
    public string Id { get; set; }
}

#endregion