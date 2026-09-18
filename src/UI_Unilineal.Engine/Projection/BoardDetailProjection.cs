using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Projection;

public sealed record IncomingSupplyProjection(
    SupplyConnection Supply,
    SourceInput? Source,
    BoardInput? OriginBoard,
    CircuitInput? ThroughCircuit,
    ProjectionStatus Status);

public sealed record BusProjection(
    BusInput Bus,
    IReadOnlyList<ProtectionInput> MainProtections,
    ProjectionStatus Status,
    int IssueCount);

public sealed record BranchDestination(
    DestinationKind Kind,
    EntityReference? Entity,
    string Label,
    EntityReference? NavigationTarget);

public sealed record BranchProjection(
    CircuitInput Circuit,
    IReadOnlyList<ProtectionInput> ProtectionChain,
    ElectricalResultInput? Result,
    BranchDestination Destination,
    BranchKind Kind,
    ProjectionStatus Status,
    IReadOnlyList<ProjectionIssue> Issues);

public sealed record BoardDetailProjection(
    BoardInput Board,
    IReadOnlyList<IncomingSupplyProjection> IncomingSupplies,
    BusProjection MainBus,
    IReadOnlyList<BranchProjection> Branches,
    IReadOnlyList<GroundingInput> Grounding,
    IReadOnlyList<ProjectionIssue> Issues,
    IReadOnlyList<EntityReference> NavigationTargets);
