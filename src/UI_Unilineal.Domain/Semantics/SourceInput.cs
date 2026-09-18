namespace UI_Unilineal.Domain.Semantics;

public sealed record SourceInput(
    EntityUid Uid,
    string Code,
    string Name,
    SourceKind Kind,
    string? SystemCode,
    decimal? NominalVoltageV,
    int? PhaseCount,
    bool? NeutralAvailable,
    OperationalState State,
    DataState DataState);
