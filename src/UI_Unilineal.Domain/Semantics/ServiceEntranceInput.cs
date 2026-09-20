namespace UI_Unilineal.Domain.Semantics;

public enum MeterKind
{
    SinglePhase,
    ThreePhase,
    Other,
    Unknown
}

public enum InputValueAuthority
{
    Manual,
    Suggested,
    Computed
}

public sealed record ServiceEntranceInput(
    EntityUid Uid,
    EntityUid SourceUid,
    MeterKind MeterKind,
    string? TariffCode,
    string? Details,
    EntityUid? ProtectionUid,
    InputValueAuthority ProtectionAuthority,
    OperationalState State,
    DataState DataState);
