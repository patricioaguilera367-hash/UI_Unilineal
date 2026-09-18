namespace UI_Unilineal.Domain.Semantics;

public sealed record BoardInput(
    EntityUid Uid,
    int Number,
    string Code,
    string Name,
    BoardRole Role,
    string? Location,
    decimal? NominalVoltageV,
    int? PhaseCount,
    OperationalState State,
    DataState DataState);
