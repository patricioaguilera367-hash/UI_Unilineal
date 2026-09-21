using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.V2;

/// <summary>
/// Minimal renderer-facing contract for one board. This is a read model, not a
/// second electrical source of truth. Absent or ambiguous data stays explicit
/// (null / Unknown / 0) instead of being guessed.
/// </summary>
public sealed record BoardDiagramModel(
    EntityUid BoardUid,
    string Code,
    string Name,
    string State,
    int MainProtectionPoles,
    string? MainProtectionLabel,
    IReadOnlyList<BoardDiagramIncomingModel> IncomingSupplies,
    IReadOnlyList<BoardDiagramBranchModel> Branches);

public sealed record BoardDiagramIncomingModel(
    EntityUid SupplyUid,
    EntityUid FeederCircuitUid,
    EntityUid OriginBoardUid,
    string OriginBoardCode,
    string OriginBoardName,
    string Role,
    bool IsNormallyActive,
    string State);

public enum BoardDiagramPresence
{
    Unknown,
    Present,
    Absent
}

public enum BoardDiagramTargetKind
{
    FinalLoad,
    DownstreamBoard,
    Unknown
}

public sealed record BoardDiagramTargetModel(
    EntityUid Uid,
    EntityKind EntityKind,
    BoardDiagramTargetKind Kind,
    string Code,
    string Name);

public sealed record BoardDiagramBranchModel(
    EntityUid CircuitUid,
    int Number,
    string Code,
    string Name,
    string SystemCode,
    string State,
    string DataState,
    string? BreakerLabel,
    bool DifferentialEnabled,
    string? DifferentialLabel,
    string? ConductorLabel,
    BoardDiagramPresence NeutralPresence,
    string? NeutralLabel,
    BoardDiagramPresence ProtectiveEarthPresence,
    string? Descriptor,
    IReadOnlyList<BoardDiagramTargetModel> Targets,
    string ResultStatus)
{
    public bool RequiresJunctionBus => Targets.Count > 1;
}
