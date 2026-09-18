namespace UI_Unilineal.Domain.Semantics;

public sealed record SupplyConnection(
    EntityUid Uid,
    EntityReference Origin,
    EntityUid? ThroughCircuitUid,
    EntityUid DestinationBoardUid,
    SupplyRole Role,
    int Priority,
    bool IsNormallyActive,
    OperationalState State,
    DataState DataState);
