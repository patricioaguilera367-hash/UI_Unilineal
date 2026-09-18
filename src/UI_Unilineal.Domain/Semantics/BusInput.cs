namespace UI_Unilineal.Domain.Semantics;

public sealed record BusInput(
    EntityUid Uid,
    EntityUid BoardUid,
    string Code,
    BusRole Role,
    decimal? RatedCurrentA,
    OperationalState State,
    DataState DataState);
