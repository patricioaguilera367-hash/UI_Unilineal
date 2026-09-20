using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class BoardDetailLayoutStrategy : ISingleLineLayoutStrategy
{
    private const double SemanticBranchWidthMm = 4;

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

        Ric18BoardLayoutTokens tokens =
            Ric18BoardLayoutTokens.From(profile);

        CompositionBlock boardFrame =
            SingleByRole(composition, "BoardFrame");
        CompositionBlock[] incoming =
            composition.Blocks
                .Where(block =>
                    block.SemanticRole is
                        "IncomingSupply" or
                        "ServiceEntranceAssembly")
                .OrderBy(block => block.Id, StringComparer.Ordinal)
                .ToArray();
        CompositionBlock[] mainProtections =
            ByRole(composition, "MainProtection");
        CompositionBlock bus =
            SingleByRole(composition, "MainBus");
        CompositionBlock neutralBus =
            SingleByRole(composition, "NeutralBus");
        CompositionBlock peBus =
            SingleByRole(composition, "ProtectiveEarthBus");
        CompositionBlock[] branches =
            ByRole(composition, "CircuitBranch");

        BranchColumn[] columns =
            branches
                .Select(branch =>
                    MeasureColumn(
                        composition,
                        measurement,
                        branch,
                        tokens))
                .ToArray();

        double incomingStackBottom =
            StackBottom(
                profile.GridMm,
                incoming
                    .Select(block =>
                        measurement.GetBlock(block.Id).Size.Height)
                    .ToArray(),
                tokens.ElementGapMm);

        double mainProtectionStackHeight =
            StackHeight(
                mainProtections
                    .Select(block =>
                        measurement.GetBlock(block.Id).Size.Height)
                    .ToArray(),
                tokens.ElementGapMm);

        double centeredHeaderLeftExtentMm =
            mainProtections
                .Select(block =>
                {
                    MeasuredBlock measured =
                        measurement.GetBlock(block.Id);
                    return measured.PowerAxisOffsetMm ??
                        (measured.Size.Width / 2.0);
                })
                .DefaultIfEmpty(0)
                .Max();
        double centeredHeaderRightExtentMm =
            mainProtections
                .Select(block =>
                {
                    MeasuredBlock measured =
                        measurement.GetBlock(block.Id);
                    double axisOffset =
                        measured.PowerAxisOffsetMm ??
                        (measured.Size.Width / 2.0);
                    return measured.Size.Width -
                        axisOffset;
                })
                .DefaultIfEmpty(0)
                .Max();

        double maximumBranchContentHeight =
            columns.Length == 0
                ? 0
                : columns.Max(column =>
                    column.InternalContentHeightMm);

        Ric18BoardGeometry geometry =
            new Ric18BoardGeometryPlanner().Plan(
                profile,
                tokens,
                columns.Length,
                columns
                    .Select(column => column.RequiredWidthMm)
                    .ToArray(),
                measurement.GetBlock(bus.Id).Size,
                measurement.GetBlock(peBus.Id).Size,
                measurement.GetBlock(neutralBus.Id).Size,
                incomingStackBottom,
                mainProtectionStackHeight,
                centeredHeaderLeftExtentMm,
                centeredHeaderRightExtentMm,
                maximumBranchContentHeight);

        var positioned =
            new List<PositionedCompositionBlock>();
        var assigned =
            new HashSet<string>(StringComparer.Ordinal);
        var externalDestinations =
            new List<(CompositionBlock Block, double PowerAxisX)>();
        double maxRight = 0;
        double maxBottom = 0;

        double incomingY = profile.GridMm;
        foreach (CompositionBlock block in incoming)
        {
            MeasuredBlock measured =
                measurement.GetBlock(block.Id);

            AddOnPowerAxis(
                block.Id,
                geometry.CenterX,
                incomingY,
                measured,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            incomingY +=
                measured.Size.Height +
                tokens.ElementGapMm;
        }

        Add(
            peBus.Id,
            geometry.ProtectiveEarthBounds,
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        Add(
            neutralBus.Id,
            geometry.NeutralBounds,
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double mainProtectionY =
            geometry.ContentTop;

        foreach (CompositionBlock block in mainProtections)
        {
            MeasuredBlock measured =
                measurement.GetBlock(block.Id);

            AddOnPowerAxis(
                block.Id,
                geometry.CenterX,
                mainProtectionY,
                measured,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            mainProtectionY +=
                measured.Size.Height +
                tokens.ElementGapMm;
        }

        Add(
            bus.Id,
            geometry.MainBusBounds,
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        for (int index = 0; index < columns.Length; index++)
        {
            BranchColumn column =
                columns[index];
            double powerAxisX =
                geometry.CircuitAxes[index];

            Add(
                column.Branch.Id,
                new MmRect(
                    powerAxisX -
                    (SemanticBranchWidthMm / 2.0),
                    geometry.BranchY,
                    SemanticBranchWidthMm,
                    tokens.BranchStubHeightMm),
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            double itemY =
                geometry.BranchContentY;

            foreach (CompositionBlock block in column.Children)
            {
                if (IsExternalDestination(block))
                {
                    externalDestinations.Add(
                        (block, powerAxisX));
                    continue;
                }

                MeasuredBlock measured =
                    measurement.GetBlock(block.Id);

                AddOnPowerAxis(
                    block.Id,
                    powerAxisX,
                    itemY,
                    measured,
                    positioned,
                    assigned,
                    ref maxRight,
                    ref maxBottom);

                itemY +=
                    measured.Size.Height +
                    tokens.ElementGapMm;
            }
        }

        Add(
            boardFrame.Id,
            new MmRect(
                geometry.BoardLeft,
                geometry.BoardTop,
                geometry.BoardWidth,
                geometry.BoardBottom -
                geometry.BoardTop),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        foreach ((CompositionBlock block, double powerAxisX) in
                 externalDestinations
                     .OrderBy(
                         item => item.Block.Id,
                         StringComparer.Ordinal))
        {
            MeasuredBlock measured =
                measurement.GetBlock(block.Id);

            AddOnPowerAxis(
                block.Id,
                powerAxisX,
                geometry.ExternalDestinationY,
                measured,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);
        }

        // Future extensions stay outside the RIC18 core so they cannot deform
        // its power axes or hierarchy.
        double auxiliaryY =
            maxBottom +
            profile.ContinuationRowGapMm;
        double auxiliaryX =
            geometry.BoardLeft;

        foreach (CompositionBlock block in composition.Blocks
                     .Where(block =>
                         !assigned.Contains(block.Id))
                     .OrderBy(
                         block => block.Id,
                         StringComparer.Ordinal))
        {
            MmSize size =
                measurement.GetBlock(block.Id).Size;

            Add(
                block.Id,
                new MmRect(
                    auxiliaryX,
                    auxiliaryY,
                    size.Width,
                    size.Height),
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            auxiliaryX +=
                size.Width +
                profile.BranchGapMm;
        }

        if (assigned.Count != composition.Blocks.Count)
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

    private static BranchColumn MeasureColumn(
        DrawingComposition composition,
        CompositionMeasurement measurement,
        CompositionBlock branch,
        Ric18BoardLayoutTokens tokens)
    {
        CompositionBlock[] children =
            composition.Blocks
                .Where(block =>
                    string.Equals(
                        block.ParentId,
                        branch.Id,
                        StringComparison.Ordinal))
                .OrderBy(ChildOrder)
                .ThenBy(
                    block => block.Id,
                    StringComparer.Ordinal)
                .ToArray();

        double requiredWidth =
            children.Length == 0
                ? tokens.MinimumCircuitPitchMm
                : children
                    .Select(block =>
                        measurement.GetBlock(block.Id).Size.Width)
                    .Max();

        double internalContentHeight =
            StackHeight(
                children
                    .Where(block =>
                        !IsExternalDestination(block))
                    .Select(block =>
                        measurement.GetBlock(block.Id).Size.Height)
                    .ToArray(),
                tokens.ElementGapMm);

        return new BranchColumn(
            branch,
            children,
            requiredWidth,
            internalContentHeight);
    }

    private static double StackBottom(
        double startY,
        IReadOnlyList<double> heights,
        double gap)
    {
        if (heights.Count == 0)
        {
            return startY;
        }

        return
            startY +
            StackHeight(
                heights,
                gap);
    }

    private static double StackHeight(
        IReadOnlyList<double> heights,
        double gap)
    {
        if (heights.Count == 0)
        {
            return 0;
        }

        return
            heights.Sum() +
            (gap *
             Math.Max(
                 0,
                 heights.Count - 1));
    }

    private static bool IsExternalDestination(
        CompositionBlock block) =>
        block.SemanticRole is
            "DownstreamBoard" or
            "FinalLoad" or
            "Unknown";

    private static void AddOnPowerAxis(
        string blockId,
        double powerAxisX,
        double y,
        MeasuredBlock measured,
        ICollection<PositionedCompositionBlock> positioned,
        ISet<string> assigned,
        ref double maxRight,
        ref double maxBottom)
    {
        double axisOffset =
            measured.PowerAxisOffsetMm ??
            (measured.Size.Width / 2.0);

        Add(
            blockId,
            new MmRect(
                powerAxisX -
                axisOffset,
                y,
                measured.Size.Width,
                measured.Size.Height),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);
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
            .OrderBy(
                block => block.Id,
                StringComparer.Ordinal)
            .ToArray();

    private static CompositionBlock SingleByRole(
        DrawingComposition composition,
        string role)
    {
        CompositionBlock[] blocks =
            ByRole(
                composition,
                role);

        if (blocks.Length != 1)
        {
            throw new InvalidOperationException(
                $"Board-detail composition must contain exactly one '{role}' block; found {blocks.Length}.");
        }

        return blocks[0];
    }

    private static int ChildOrder(
        CompositionBlock block) =>
        block.SemanticRole switch
        {
            "Protection" => 0,
            "DifferentialProtection" => 1,
            "DownstreamBoard" => 2,
            "FinalLoad" => 2,
            "Unknown" => 2,
            _ => 3
        };

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
            new PositionedCompositionBlock(
                blockId,
                bounds));

        maxRight =
            Math.Max(
                maxRight,
                bounds.Right);
        maxBottom =
            Math.Max(
                maxBottom,
                bounds.Bottom);
    }

    private sealed record BranchColumn(
        CompositionBlock Branch,
        IReadOnlyList<CompositionBlock> Children,
        double RequiredWidthMm,
        double InternalContentHeightMm);
}
