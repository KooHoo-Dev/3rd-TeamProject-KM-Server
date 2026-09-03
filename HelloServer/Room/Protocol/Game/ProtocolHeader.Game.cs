namespace HelloServer;

public static partial class ProtocolHeader
{
    #region CLIENT_TO_SERVER
    
    public const string SET_BOARD_READY = "setBoardReady";
    
    public const string ROLL_DICE = "rollDice";
    public const string BUY_OR_SELL_TERRITORY = "buyOrSellTerritory";
    public const string UPDATE_ECONOMY = "updateEconomy";

    public const string DRAW_GOLD_CARD = "drawGoldCard";
    public const string MOVE_USER_TO = "moveUserTo";
    public const string USER_MOVE_FINISHED = "userMoveFinished";
    public const string ADD_INCAPACITATION_COUNT = "addIncapacitationCount";
    
    public const string TURN_FINISHED = "turnFinished";

    #endregion

    #region SERVER_TO_CLIENT
    
    public const string TURN_STARTED = "turnStarted";
    public const string DICE_ROLLED = "diceRolled";
    public const string ECONOMY_UPDATED = "economyUpdated";
    public const string TERRITORY_UPDATED = "territoryUpdated";
    
    public const string GOLD_CARD_DRAWN = "goldCardDrawn";
    public const string USER_MOVED_TO = "userMovedTo";
    
    public const string GAME_ENDED = "gameEnded";

    #endregion
}