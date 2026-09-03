namespace HelloServer;

public class User
{
    public string Id { get; set; }
    public string Nickname { get; set; }

    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
}

public class UserEconomy
{
    public string UserId { get; set; }
    public int Gold { get; set; }
}

public class UserTerritory
{
    public string UserId { get; set; }

    public int SpaceId { get; set; }
    public bool IsBuilt { get; set; }
    public bool IsLandmark { get; set; }
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

public class SetGameReadyMessage
{
    public string Type { get; set; } = ProtocolHeader.SET_GAME_READY;
    public bool IsReady { get; set; }
}

public class StartGameMessage
{
    public string Type { get; set; } = ProtocolHeader.START_GAME;
}

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

public struct EconomyUpdate
{
    public string UserId { get; set; }
    public int Amount { get; set; }
}

public class BuyOrSellTerritoryMessage
{
    public string Type { get; set; } = ProtocolHeader.BUY_OR_SELL_TERRITORY;

    public int TurnId { get; set; }
    public int SpaceId { get; set; }
    public bool IsBuy { get; set; } // true = buy, false = sell
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

public class TurnStartedMessage
{
    public string Type { get; set; } = ProtocolHeader.TURN_STARTED;
    
    public int RoundCount { get; set; }
    public int TurnId { get; set; }
    public string PlayerId { get; set; }
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

public class TerritoryUpdatedMessage
{
    public string Type { get; set; } = ProtocolHeader.TERRITORY_UPDATED;
    public UserTerritory[] Territories { get; set; }
}

#endregion