using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class SummaryLayoutStrategy : ISingleLineLayoutStrategy
{
    public PositionedLayout Layout(
        DrawingComposition composition,
        CompositionMeasurement measurement,
        LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(profile);

        if (composition.Kind != DrawingCompositionKind.Summary)
        {
            throw new ArgumentException(
                "SummaryLayoutStrategy requires a summary composition.",
                nameof(composition));
        }

        if (composition.Blocks.Count == 0)
        {
            throw new InvalidOperationException(
                "Summary composition does not contain any blocks.");
        }

        Dictionary<string, CompositionBlock> blocksById =
            composition.Blocks.ToDictionary(
                block => block.Id,
                StringComparer.Ordinal);

        Dictionary<string, int> depths =
            ComputePrimaryDepths(composition, blocksById);

        Dictionary<int, string[]> blocksByDepth = depths
            .GroupBy(pair => pair.Value)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(pair => pair.Key)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray());

        int maxDepth = blocksByDepth.Keys.Max();

        var columnWidths = new Dictionary<int, double>();
        for (int depth = 0; depth <= maxDepth; depth++)
        {
            if (!blocksByDepth.TryGetValue(
                    depth,
                    out string[]? ids) ||
                ids.Length == 0)
            {
                columnWidths[depth] = 0;
                continue;
            }

            columnWidths[depth] = ids
                .Select(id => measurement.GetBlock(id).Size.Width)
                .Max();
        }

        var xByDepth = new Dictionary<int, double>();
        double x = profile.GridMm;

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            xByDepth[depth] = x;
            x += columnWidths[depth] + profile.HorizontalGapMm;
        }

        var positioned = new List<PositionedCompositionBlock>();
        double maxRight = 0;
        double maxBottom = 0;

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            if (!blocksByDepth.TryGetValue(
                    depth,
                    out string[]? ids))
            {
                continue;
            }

            double y = profile.GridMm;

            foreach (string id in ids)
            {
                MmSize size = measurement.GetBlock(id).Size;
                var bounds = new MmRect(
                    xByDepth[depth],
                    y,
                    size.Width,
                    size.Height);

                positioned.Add(
                    new PositionedCompositionBlock(id, bounds));

                maxRight = Math.Max(maxRight, bounds.Right);
                maxBottom = Math.Max(maxBottom, bounds.Bottom);
                y = bounds.Bottom + profile.VerticalGapMm;
            }
        }

        return new PositionedLayout(
            positioned,
            new MmRect(
                0,
                0,
                maxRight + profile.GridMm,
                maxBottom + profile.GridMm));
    }

    private static Dictionary<string, int> ComputePrimaryDepths(
        DrawingComposition composition,
        IReadOnlyDictionary<string, CompositionBlock> blocks)
    {
        string[] ids = blocks.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var indegree = ids.ToDictionary(
            id => id,
            _ => 0,
            StringComparer.Ordinal);
        var outgoing = ids.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (CompositionConnection connection in composition.Connections
                     .Where(connection =>
                         string.Equals(
                             connection.LineStyleId,
                             "POWER",
                             StringComparison.Ordinal))
                     .OrderBy(
                         connection => connection.Id,
                         StringComparer.Ordinal))
        {
            string source = connection.Source.BlockId;
            string target = connection.Target.BlockId;

            if (!blocks.ContainsKey(source) ||
                !blocks.ContainsKey(target))
            {
                throw new InvalidOperationException(
                    $"Primary connection '{connection.Id}' references a missing block.");
            }

            outgoing[source].Add(target);
            indegree[target]++;
        }

        foreach (List<string> targets in outgoing.Values)
        {
            targets.Sort(StringComparer.Ordinal);
        }

        var depths = ids.ToDictionary(
            id => id,
            _ => 0,
            StringComparer.Ordinal);

        var ready = new SortedSet<string>(
            indegree
                .Where(pair => pair.Value == 0)
                .Select(pair => pair.Key),
            StringComparer.Ordinal);

        int processed = 0;

        while (ready.Count > 0)
        {
            string current = ready.Min!;
            ready.Remove(current);
            processed++;

            foreach (string target in outgoing[current])
            {
                depths[target] = Math.Max(
                    depths[target],
                    depths[current] + 1);

                indegree[target]--;

                if (indegree[target] == 0)
                {
                    ready.Add(target);
                }
            }
        }

        if (processed != ids.Length)
        {
            throw new InvalidOperationException(
                "Primary summary topology contains a cycle.");
        }

        return depths;
    }
}
