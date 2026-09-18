namespace UI_Unilineal.Domain.Semantics;

public sealed record CircuitInput(
    EntityUid Uid,
    EntityUid BoardUid,
    int Number,
    string Code,
    string Name,
    CircuitRole Role,
    string? ServiceCode,
    string? SystemCode,
    decimal? VoltageV,
    decimal? PowerFactor,
    decimal? LengthM,
    string? RacewayCode,
    string? InstallationMethodCode,
    ConductorInput? Conductor,
    OperationalState State,
    DataState DataState);
