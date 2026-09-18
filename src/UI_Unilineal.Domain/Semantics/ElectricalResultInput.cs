namespace UI_Unilineal.Domain.Semantics;

public sealed record ElectricalResultInput(
    EntityReference Entity,
    decimal? InstalledPowerW,
    decimal? TheoreticalCurrentA,
    decimal? DesignCurrentA,
    decimal? CorrectedAmpacityA,
    decimal? VoltageDropV,
    decimal? VoltageDropPercent,
    string? GlobalStatus,
    ResultState ResultState,
    string? ExecutionReference);
