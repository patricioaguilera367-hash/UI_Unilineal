using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Projection;

internal sealed class SummaryProjectionBuilder
{
    public SummaryProjection Build(
        SingleLineInput input,
        SemanticEntityIndex index,
        IReadOnlyList<ProjectionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(issues);

        SourceInput[] sources = input.Sources
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Uid.Value, StringComparer.Ordinal)
            .ToArray();

        BoardInput[] boards = input.Boards
            .OrderBy(x => x.Number)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Uid.Value, StringComparer.Ordinal)
            .ToArray();

        SummaryNode[] nodes =
        [
            .. sources.Select(source => BuildSourceNode(source, issues)),
            .. boards.Select(board => BuildBoardNode(board, input, issues))
        ];

        SummaryConnection[] connections = input.SupplyConnections
            .OrderBy(supply => DestinationBoardNumber(index, supply))
            .ThenBy(supply => supply.Priority)
            .ThenBy(supply => supply.Role)
            .ThenBy(supply => supply.Uid.Value, StringComparer.Ordinal)
            .Select(supply => BuildConnection(supply, issues))
            .ToArray();

        HashSet<EntityUid> boardsWithActiveIncoming = input.SupplyConnections
            .Where(supply => supply.State == OperationalState.Active)
            .Select(supply => supply.DestinationBoardUid)
            .ToHashSet();

        EntityReference[] roots =
        [
            .. sources.Select(source => new EntityReference(source.Uid, EntityKind.Source)),
            .. boards
                .Where(board => !boardsWithActiveIncoming.Contains(board.Uid))
                .Select(board => new EntityReference(board.Uid, EntityKind.Board))
        ];

        return new SummaryProjection(nodes, connections, roots, issues.ToArray());
    }

    private static SummaryNode BuildSourceNode(
        SourceInput source,
        IReadOnlyList<ProjectionIssue> issues)
    {
        EntityReference entity = new(source.Uid, EntityKind.Source);
        ProjectionIssue[] entityIssues = IssuesFor(entity, issues);

        return new SummaryNode(
            entity,
            source.Code,
            source.Name,
            null,
            ProjectionStatusResolver.Resolve(source.DataState, null, entityIssues),
            0,
            entityIssues.Length);
    }

    private static SummaryNode BuildBoardNode(
        BoardInput board,
        SingleLineInput input,
        IReadOnlyList<ProjectionIssue> issues)
    {
        EntityReference entity = new(board.Uid, EntityKind.Board);
        ProjectionIssue[] entityIssues = IssuesFor(entity, issues);
        int incomingSupplyCount = input.SupplyConnections.Count(
            supply => supply.DestinationBoardUid == board.Uid);
        int alternateSupplyCount = Math.Max(0, incomingSupplyCount - 1);

        return new SummaryNode(
            entity,
            board.Code,
            board.Name,
            board.Role,
            ProjectionStatusResolver.Resolve(board.DataState, null, entityIssues),
            alternateSupplyCount,
            entityIssues.Length);
    }

    private static SummaryConnection BuildConnection(
        SupplyConnection supply,
        IReadOnlyList<ProjectionIssue> issues)
    {
        EntityReference entity = new(supply.Uid, EntityKind.SupplyConnection);
        ProjectionIssue[] entityIssues = IssuesFor(entity, issues);

        return new SummaryConnection(
            supply.Uid,
            supply.Origin,
            supply.ThroughCircuitUid,
            new EntityReference(supply.DestinationBoardUid, EntityKind.Board),
            supply.Role,
            supply.IsNormallyActive,
            ProjectionStatusResolver.Resolve(supply.DataState, null, entityIssues));
    }

    private static int DestinationBoardNumber(
        SemanticEntityIndex index,
        SupplyConnection supply) =>
        index.TryGetBoard(supply.DestinationBoardUid, out BoardInput? board) &&
        board is not null
            ? board.Number
            : int.MaxValue;

    private static ProjectionIssue[] IssuesFor(
        EntityReference entity,
        IEnumerable<ProjectionIssue> issues) =>
        issues.Where(issue => issue.Entity == entity).ToArray();
}
