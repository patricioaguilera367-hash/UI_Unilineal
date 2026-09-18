using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Projection;

public sealed record SummaryNode(
    EntityReference Entity,
    string Code,
    string Name,
    BoardRole? BoardRole,
    ProjectionStatus Status,
    int AlternateSupplyCount,
    int IssueCount);

public sealed record SummaryConnection(
    EntityUid SupplyConnectionUid,
    EntityReference Origin,
    EntityUid? ThroughCircuitUid,
    EntityReference Destination,
    SupplyRole Role,
    bool IsNormallyActive,
    ProjectionStatus Status);

public sealed record SummaryProjection(
    IReadOnlyList<SummaryNode> Nodes,
    IReadOnlyList<SummaryConnection> Connections,
    IReadOnlyList<EntityReference> Roots,
    IReadOnlyList<ProjectionIssue> Issues);
