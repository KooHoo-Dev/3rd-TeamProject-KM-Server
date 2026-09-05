namespace HelloServer;

public class UserEconomy
{
    public string UserId { get; set; }
    public int Gold { get; set; }
}

public struct EconomyUpdate
{
    public string UserId { get; set; }
    public int Amount { get; set; }
}

public enum TileEffectSyncPhase
{
    Ready = 0,
    Resolved = 1
}

#region CLIENT_TO_SERVER

public class SetBoardReadyMessage
{
    public string Type { get; set; } = ProtocolHeader.SET_BOARD_READY;
}

public class RollDiceMessage
{
    public string Type { get; set; } = ProtocolHeader.ROLL_DICE;
    public int TurnId { get; set; }
}

public class DrawGoldCardMessage
{
    public string Type { get; set; } = ProtocolHeader.DRAW_GOLD_CARD;

    public int TurnId { get; set; }
    public int[] CardIds { get; set; }
}

public class TurnFinishedMessage
{
    public string Type { get; set; } = ProtocolHeader.TURN_FINISHED;
    public int TurnId { get; set; }
}

public class UpdateEconomyMessage
{
    public string Type { get; set; } = ProtocolHeader.UPDATE_ECONOMY;
    public EconomyUpdate[] Updates { get; set; }
}

public class AddIncapacitationCountMessage
{
    public string Type { get; set; } = ProtocolHeader.ADD_INCAPACITATION_COUNT;
    
    public int Count { get; set; }
}

#endregion

#region SERVER_TO_CLIENT

public class TurnStartedMessage
{
    public string Type { get; set; } = ProtocolHeader.TURN_STARTED;
    
    public int RoundCount { get; set; }
    public int TurnId { get; set; }
    
    public string PlayerId { get; set; }
    public bool CanAct { get; set; }
}
            
public class DiceRolledMessage
{
    public string Type { get; set; } = ProtocolHeader.DICE_ROLLED;
    
    public int TurnId { get; set; }
    public string PlayerId { get; set; }
    public int D1 { get; set; }
    public int D2 { get; set; }
}

public class GoldCardDrawnMessage
{
    public string Type { get; set; } = ProtocolHeader.GOLD_CARD_DRAWN;
        
    public int TurnId { get; set; }
    public int CardId { get; set; }
    public string PlayerId { get; set; }
}

public class GameEndedMessage
{
    public string Type { get; set; } = ProtocolHeader.GAME_ENDED;
}

public class EconomyUpdatedMessage
{
    public string Type { get; set; } = ProtocolHeader.ECONOMY_UPDATED;
    public UserEconomy[] Economies { get; set; }
}

#endregion

#region BI_DIRECTIONAL

public class MoveUserToMessage
{
    public string Type { get; set; } = ProtocolHeader.MOVE_USER_TO;
        
    public int TurnId { get; set; }
    public long MoveId { get; set; }
        
    public string RequestId { get; set; }
        
    public string UserId { get; set; }
    public int TileId { get; set; }
}

public class UserMoveFinishedMessage
{
    public string Type { get; set; } = ProtocolHeader.USER_MOVE_FINISHED;
        
    public int TurnId { get; set; }
    public long MoveId { get; set; }
        
    public string RequestId { get; set; }
}

public class TileEffectSyncMessage
{
    public string Type { get; set; } = ProtocolHeader.TILE_EFFECT_SYNC;

    public int TurnId { get; set; }
    public long MoveId { get; set; }
    public int StepIndex { get; set; }
    public TileEffectSyncPhase Phase { get; set; }
}

public class UpdateTerritoryMessage
{
    public string Type { get; set; } = ProtocolHeader.UPDATE_TERRITORY;

    public int TileId { get; set; }
        
    public string OwnerId { get; set; }
    public bool HasBuilding { get; set; }
    public bool HasLandMark { get; set; }
}
    
#endregion
