namespace HelloServer;

public static class ProtocolHeader
{
    #region CLIENT_TO_SERVER

    public const string HELLO = "hello";
    public const string SET_GAME_READY = "setGameReady";
    public const string START_GAME = "startGame";
    public const string SET_BOARD_READY = "setBoardReady";
    public const string ROLL_DICE = "rollDice";
    public const string DRAW_GOLD_CARD = "drawGoldCard";
    public const string TURN_FINISHED = "turnFinished";

    #endregion

    #region SERVER_TO_CLIENT

    public const string WELCOME = "welcome";
    public const string JOIN = "join";
    public const string LEAVE = "leave";
    public const string GAME_READY_SET = "gameReadySet";
    public const string GAME_STARTED = "gameStarted";
    public const string TURN_STARTED = "turnStarted";
    public const string DICE_ROLLED = "diceRolled";
    public const string GOLD_CARD_DRAWN = "goldCardDrawn";
    public const string GAME_ENDED = "gameEnded";

    #endregion
}