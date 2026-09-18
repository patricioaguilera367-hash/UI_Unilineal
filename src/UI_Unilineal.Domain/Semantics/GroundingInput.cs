namespace UI_Unilineal.Domain.Semantics;

public sealed record GroundingInput(
    EntityUid Uid,
    EntityReference Owner,
    GroundingKind Kind,
    string? ConductorMaterialCode,
    decimal? ConductorSectionMm2,
    decimal? ResistanceOhm,
    string? MeasurementMethod,
    string? Instrument,
    OperationalState State,
    DataState DataState);
