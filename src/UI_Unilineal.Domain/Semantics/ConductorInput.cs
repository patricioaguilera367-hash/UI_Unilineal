namespace UI_Unilineal.Domain.Semantics;

public sealed record ConductorInput(
    string? MaterialCode,
    string? TypeCode,
    decimal? PhaseSectionMm2,
    decimal? NeutralSectionMm2,
    int? ActiveConductors,
    string? Description);
