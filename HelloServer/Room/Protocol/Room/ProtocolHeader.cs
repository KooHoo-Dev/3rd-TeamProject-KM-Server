namespace HelloServer;

public static partial class ProtocolHeader
{
    #region CLIENT_TO_SERVER

    public const string HELLO = "hello";

    #endregion

    #region SERVER_TO_CLIENT

    public const string WELCOME = "welcome";
    public const string JOIN = "join";
    public const string LEAVE = "leave";

    #endregion
}