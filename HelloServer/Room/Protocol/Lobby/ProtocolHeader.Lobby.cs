namespace HelloServer;

public static partial class ProtocolHeader
{
    #region CLIENT_TO_SERVER
    
    public const string SET_GAME_READY = "setGameReady";
    public const string START_GAME = "startGame";

    #endregion

    #region SERVER_TO_CLIENT
    
    public const string GAME_READY_SET = "gameReadySet";
    public const string GAME_STARTED = "gameStarted";

    #endregion
}