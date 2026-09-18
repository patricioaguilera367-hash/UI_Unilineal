using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Composition;

public static class CompositionIdFactory
{
    public static string SummaryEntity(EntityReference entity) =>
        $"summary/{KindSegment(entity.Kind)}/{entity.Uid}";

    public static string SummarySupply(EntityUid supplyUid) =>
        $"summary/supply/{supplyUid}";

    public static string DetailBoard(EntityUid boardUid) =>
        $"detail/{boardUid}";

    public static string DetailIncoming(
        EntityUid boardUid,
        EntityUid supplyUid) =>
        $"detail/{boardUid}/incoming/{supplyUid}";

    public static string DetailBus(
        EntityUid boardUid,
        EntityUid busUid) =>
        $"detail/{boardUid}/bus/{busUid}";

    public static string DetailMainProtection(
        EntityUid boardUid,
        EntityUid protectionUid) =>
        $"detail/{boardUid}/main-protection/{protectionUid}";

    public static string DetailBranch(
        EntityUid boardUid,
        EntityUid circuitUid) =>
        $"detail/{boardUid}/branch/{circuitUid}";

    public static string DetailProtection(
        EntityUid boardUid,
        EntityUid circuitUid,
        EntityUid protectionUid) =>
        $"detail/{boardUid}/branch/{circuitUid}/protection/{protectionUid}";

    public static string DetailDestination(
        EntityUid boardUid,
        EntityUid circuitUid) =>
        $"detail/{boardUid}/branch/{circuitUid}/destination";

    public static string DetailGrounding(
        EntityUid boardUid,
        EntityUid groundingUid) =>
        $"detail/{boardUid}/grounding/{groundingUid}";

    public static string DetailConnection(
        EntityUid boardUid,
        string relation,
        string identity) =>
        $"detail/{boardUid}/connection/{relation}/{identity}";

    private static string KindSegment(EntityKind kind) =>
        kind switch
        {
            EntityKind.Project => "project",
            EntityKind.Source => "source",
            EntityKind.Board => "board",
            EntityKind.Circuit => "circuit",
            EntityKind.SupplyConnection => "supply",
            EntityKind.Protection => "protection",
            EntityKind.Bus => "bus",
            EntityKind.Grounding => "grounding",
            EntityKind.Load => "load",
            _ => kind.ToString().ToLowerInvariant()
        };
}
