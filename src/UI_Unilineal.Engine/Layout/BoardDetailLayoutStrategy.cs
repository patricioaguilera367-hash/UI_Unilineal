using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class BoardDetailLayoutStrategy : ISingleLineLayoutStrategy
{
    public PositionedLayout Layout(
        DrawingComposition composition,
        CompositionMeasurement measurement,
        LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(measurement);
        ArgumentNullException.ThrowIfNull(profile);

        if (composition.Kind != DrawingCompositionKind.BoardDetail)
        {
            throw new ArgumentException(
                "BoardDetailLayoutStrategy requires a board-detail composition.",
                nameof(composition));
        }

        if (composition.Blocks.Count == 0)
        {
            throw new InvalidOperationException(
                "Board-detail composition does not contain any blocks.");
        }

        Dictionary<string, CompositionBlock> blocksById =
            composition.Blocks.ToDictionary(
                block => block.Id,
                StringComparer.Ordinal);

        CompositionBlock[] incoming = ByRole(composition, "IncomingSupply");
        CompositionBlock[] mainProtections =
            ByRole(composition, "MainProtection");
        CompositionBlock[] buses = ByRole(composition, "MainBus");

        if (buses.Length != 1)
        {
            throw new InvalidOperationException(
                $"Board-detail composition must contain exactly one main bus; found {buses.Length}.");
        }

        CompositionBlock bus = buses[0];
        var positioned = new List<PositionedCompositionBlock>();
        var assigned = new HashSet<string>(StringComparer.Ordinal);

        double maxRight = 0;
        double maxBottom = 0;

        double mainX = profile.GridMm;
        double y = profile.GridMm;

        foreach (CompositionBlock block in incoming)
        {
            PlaceVertical(
                block,
                mainX,
                ref y,
                profile.VerticalGapMm,
                measurement,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);
        }

        foreach (CompositionBlock block in mainProtections)
        {
            PlaceVertical(
                block,
                mainX,
                ref y,
                profile.VerticalGapMm,
                measurement,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);
        }

        PlaceVertical(
            bus,
            mainX,
            ref y,
            profile.VerticalGapMm,
            measurement,
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        PositionedCompositionBlock busPosition =
            positioned.Single(item => item.BlockId == bus.Id);

        CompositionBlock[] branches = ByRole(composition, "CircuitBranch");
        CompositionBlock[] neutralBuses = ByRole(composition, "NeutralBus");
        CompositionBlock[] peBuses = ByRole(composition, "ProtectiveEarthBus");

        bool hasStructuralRails =
            neutralBuses.Length > 0 ||
            peBuses.Length > 0;

        if (hasStructuralRails &&
            (neutralBuses.Length != 1 || peBuses.Length != 1))
        {
            throw new InvalidOperationException(
                "Board-detail composition must contain both neutral and protective-earth buses when structural rails are present.");
        }

        double branchSpan =
            branches.Length == 0
                ? busPosition.Bounds.Width
                : branches
                    .Select(branch =>
                    {
                        CompositionBlock[] children = composition.Blocks
                            .Where(block =>
                                string.Equals(
                                    block.ParentId,
                                    branch.Id,
                                    StringComparison.Ordinal))
                            .OrderBy(ChildOrder)
                            .ThenBy(block => block.Id, StringComparer.Ordinal)
                            .ToArray();

                        CompositionBlock[] column =
                            [branch, .. children];

                        return column
                            .Select(block =>
                                measurement.GetBlock(block.Id).Size.Width)
                            .Max();
                    })
                    .Sum() +
                (profile.BranchGapMm * Math.Max(0, branches.Length - 1));

        double railWidth =
            Math.Max(
                busPosition.Bounds.Width,
                Math.Min(
                    profile.MaxBoardDetailWidthMm,
                    branchSpan));

        double railY =
            busPosition.Bounds.Bottom +
            profile.VerticalGapMm;

        if (hasStructuralRails)
        {
            foreach (CompositionBlock rail in neutralBuses.Concat(peBuses))
            {
                MmSize measured =
                    measurement.GetBlock(rail.Id).Size;
                var bounds =
                    new MmRect(
                        profile.GridMm,
                        railY,
                        railWidth,
                        measured.Height);

                Add(
                    rail.Id,
                    bounds,
                    positioned,
                    assigned,
                    ref maxRight,
                    ref maxBottom);

                railY =
                    bounds.Bottom +
                    profile.VerticalGapMm;
            }
        }

        double rowY = railY;
        double currentX = profile.GridMm;
        double rowMaxHeight = 0;
        bool rowHasBranch = false;

        foreach (CompositionBlock branch in branches)
        {
            CompositionBlock[] children = composition.Blocks
                .Where(block =>
                    string.Equals(
                        block.ParentId,
                        branch.Id,
                        StringComparison.Ordinal))
                .OrderBy(ChildOrder)
                .ThenBy(block => block.Id, StringComparer.Ordinal)
                .ToArray();

            CompositionBlock[] column =
                [branch, .. children];

            double columnWidth = column
                .Select(block => measurement.GetBlock(block.Id).Size.Width)
                .Max();
            double columnHeight = column
                .Select(block => measurement.GetBlock(block.Id).Size.Height)
                .Sum() +
                (profile.VerticalGapMm * Math.Max(0, column.Length - 1));

            double maxAllowedRight =
                profile.MaxBoardDetailWidthMm + profile.GridMm;

            if (rowHasBranch &&
                currentX + columnWidth > maxAllowedRight)
            {
                rowY += rowMaxHeight + profile.ContinuationRowGapMm;
                currentX = profile.GridMm;
                rowMaxHeight = 0;
                rowHasBranch = false;
            }

            double itemY = rowY;

            foreach (CompositionBlock block in column)
            {
                MmSize size = measurement.GetBlock(block.Id).Size;
                var bounds = new MmRect(
                    currentX,
                    itemY,
                    size.Width,
                    size.Height);

                Add(
                    block.Id,
                    bounds,
                    positioned,
                    assigned,
                    ref maxRight,
                    ref maxBottom);

                itemY = bounds.Bottom + profile.VerticalGapMm;
            }

            rowMaxHeight = Math.Max(rowMaxHeight, columnHeight);
            currentX += columnWidth + profile.BranchGapMm;
            rowHasBranch = true;
        }

        double auxiliaryY = branches.Length > 0
            ? rowY + rowMaxHeight + profile.ContinuationRowGapMm
            : busPosition.Bounds.Bottom + profile.VerticalGapMm;

        CompositionBlock[] auxiliary = composition.Blocks
            .Where(block => !assigned.Contains(block.Id))
            .OrderBy(block => block.Id, StringComparer.Ordinal)
            .ToArray();

        double auxiliaryX = profile.GridMm;

        foreach (CompositionBlock block in auxiliary)
        {
            MmSize size = measurement.GetBlock(block.Id).Size;

            if (auxiliaryX > profile.GridMm &&
                auxiliaryX + size.Width >
                profile.MaxBoardDetailWidthMm + profile.GridMm)
            {
                auxiliaryX = profile.GridMm;
                auxiliaryY =
                    maxBottom + profile.ContinuationRowGapMm;
            }

            var bounds = new MmRect(
                auxiliaryX,
                auxiliaryY,
                size.Width,
                size.Height);

            Add(
                block.Id,
                bounds,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            auxiliaryX =
                bounds.Right + profile.BranchGapMm;
        }

        if (assigned.Count != blocksById.Count)
        {
            throw new InvalidOperationException(
                "Board-detail layout did not position every composition block.");
        }

        return new PositionedLayout(
            positioned,
            new MmRect(
                0,
                0,
                maxRight + profile.GridMm,
                maxBottom + profile.GridMm));
    }

    private static CompositionBlock[] ByRole(
        DrawingComposition composition,
        string role) =>
        composition.Blocks
            .Where(block =>
                string.Equals(
                    block.SemanticRole,
                    role,
                    StringComparison.Ordinal))
            .OrderBy(block => block.Id, StringComparer.Ordinal)
            .ToArray();

    private static int ChildOrder(CompositionBlock block) =>
        block.SemanticRole switch
        {
            "DifferentialProtection" => 0,
            "Protection" => 0,
            "DownstreamBoard" => 1,
            "FinalLoad" => 1,
            "Unknown" => 1,
            _ => 2
        };

    private static void PlaceVertical(
        CompositionBlock block,
        double x,
        ref double y,
        double gap,
        CompositionMeasurement measurement,
        ICollection<PositionedCompositionBlock> positioned,
        ISet<string> assigned,
        ref double maxRight,
        ref double maxBottom)
    {
        MmSize size = measurement.GetBlock(block.Id).Size;
        var bounds = new MmRect(
            x,
            y,
            size.Width,
            size.Height);

        Add(
            block.Id,
            bounds,
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        y = bounds.Bottom + gap;
    }

    private static void Add(
        string blockId,
        MmRect bounds,
        ICollection<PositionedCompositionBlock> positioned,
        ISet<string> assigned,
        ref double maxRight,
        ref double maxBottom)
    {
        if (!assigned.Add(blockId))
        {
            throw new InvalidOperationException(
                $"Composition block '{blockId}' was positioned more than once.");
        }

        positioned.Add(
            new PositionedCompositionBlock(blockId, bounds));
        maxRight = Math.Max(maxRight, bounds.Right);
        maxBottom = Math.Max(maxBottom, bounds.Bottom);
    }
}
