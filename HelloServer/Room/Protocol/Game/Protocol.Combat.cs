namespace HelloServer;

public class CombatRequestMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_REQUEST;
    public string RequestId { get; set; }
    public int TurnId { get; set; }
    public int TileId { get; set; }
    public string DefenderId { get; set; }
}

public class CombatOfferMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_OFFER;
    public string CombatId { get; set; }
    public string RequestId { get; set; }
    public int TurnId { get; set; }
    public int TileId { get; set; }
    public string AttackerId { get; set; }
    public string DefenderId { get; set; }
    public double RejectionChance { get; set; } = 0.5;
}

public class CombatResponseMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_RESPONSE;
    public string CombatId { get; set; }
    public bool Accepted { get; set; }
}

public class CombatCancelledMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_CANCELLED;
    public string CombatId { get; set; }
    public string RequestId { get; set; }
    public string Reason { get; set; }
}

public class CombatAssignmentMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_ASSIGNMENT;
    public string CombatId { get; set; }
    public string RequestId { get; set; }
    public string AttackerId { get; set; }
    public string DefenderId { get; set; }
    public int StartHp { get; set; }
    public float DurationSeconds { get; set; }
    public bool IsTest { get; set; }
}

public class CombatReadyMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_READY;
    public string CombatId { get; set; }
}

public class CombatStartedMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_STARTED;
    public string CombatId { get; set; }
}

public class CombatPositionMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_POSITION;
    public string CombatId { get; set; }
    public string PlayerId { get; set; }
    public float X { get; set; }
}

public class SkillCastMessage
{
    public string Type { get; set; } = ProtocolHeader.SKILL_CAST;
    public string CombatId { get; set; }
    public string CastId { get; set; }
    public string CasterId { get; set; }
    public bool IsBasicAttack { get; set; }
    public int EquipmentId { get; set; }
}

public class CombatHealthReportMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_HEALTH_REPORT;
    public string CombatId { get; set; }
    public int RemainingHp { get; set; }
}

public class CombatHealthUpdatedMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_HEALTH_UPDATED;
    public string CombatId { get; set; }
    public string PlayerId { get; set; }
    public int RemainingHp { get; set; }
}

public class CombatResultMessage
{
    public string Type { get; set; } = ProtocolHeader.COMBAT_RESULT;
    public string CombatId { get; set; }
    public string WinnerId { get; set; }
    public string LoserId { get; set; }
}
