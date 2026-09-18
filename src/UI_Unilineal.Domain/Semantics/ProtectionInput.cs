namespace UI_Unilineal.Domain.Semantics;

public sealed record ProtectionInput(
    EntityUid Uid,
    EntityReference Owner,
    EntityReference Protects,
    ProtectionKind Kind,
    ProtectionRole Role,
    int? Poles,
    decimal? RatedCurrentA,
    decimal? BreakingCapacityKa,
    string? Curve,
    decimal? DifferentialCurrentMa,
    string? DifferentialType,
    string? Manufacturer,
    string? Model,
    OperationalState State,
    DataState DataState);
