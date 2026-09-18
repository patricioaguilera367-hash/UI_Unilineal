namespace UI_Unilineal.Domain.Semantics;

public enum SourceKind
{
    Utility,
    Transformer,
    Generator,
    Battery,
    Ups,
    Alternative,
    Unknown
}

public enum BoardRole
{
    Main,
    Distribution,
    SubDistribution,
    Control,
    Emergency,
    Other,
    Unknown
}

public enum CircuitRole
{
    Final,
    Feeder,
    Subfeeder,
    Reserved,
    Other,
    Unknown
}

public enum BusRole
{
    Main,
    Section,
    Other
}

public enum SupplyRole
{
    Normal,
    Emergency,
    Alternative,
    Bypass,
    TransferInput,
    Unknown
}

public enum ProtectionKind
{
    Breaker,
    Differential,
    Fuse,
    Combined,
    Other,
    Unknown
}

public enum ProtectionRole
{
    Main,
    Branch,
    Feeder,
    Backup,
    Recommended,
    Adopted,
    Other
}

public enum GroundingKind
{
    Protection,
    Service,
    Functional,
    Combined,
    Unknown
}
