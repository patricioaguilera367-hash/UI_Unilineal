using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class BoardDetailLayoutStrategy : ISingleLineLayoutStrategy
{
    private const double BoardSideMarginMm = 8;
    private const double BoardHeaderHeightMm = 15;
    private const double InternalVerticalGapMm = 3;
    private const double MinimumBoardWidthMm = 80;
    private const double MinimumSlotWidthMm = 34;
    private const double SemanticBranchWidthMm = 4;
    private const double SemanticBranchHeightMm = 2;
    private const double MainBusVisualHeightMm = 6;

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

        var positioned = new List<PositionedCompositionBlock>();
        var assigned = new HashSet<string>(StringComparer.Ordinal);
        var externalDestinations =
            new List<(CompositionBlock Block, double PowerAxisX)>();
        double maxRight = 0;
        double maxBottom = 0;

        BranchColumn[] columns =
            branches
                .Select(branch =>
                    MeasureColumn(
                        composition,
                        measurement,
                        branch))
                .ToArray();

        double requiredColumnWidth =
            columns.Length == 0
                ? MinimumSlotWidthMm
                : columns.Max(column => column.RequiredWidthMm);
        double slotWidth =
            Math.Max(
                MinimumSlotWidthMm,
                requiredColumnWidth + profile.BranchGapMm);

        double branchAreaWidth =
            Math.Max(
                measurement.GetBlock(bus.Id).Size.Width,
                Math.Max(1, columns.Length) * slotWidth);
        double boardWidth =
            Math.Max(
                MinimumBoardWidthMm,
                branchAreaWidth + (BoardSideMarginMm * 2));
        double boardLeft = profile.GridMm;
        double branchAreaLeft =
            boardLeft + BoardSideMarginMm;
        double centerX =
            boardLeft + (boardWidth / 2.0);

        // Incoming supply/EMPALME is external to the board and its electrical
        // power axis is kept collinear with the board incoming path.
        double y = profile.GridMm;
        foreach (CompositionBlock block in incoming)
        {
            MeasuredBlock measured =
                measurement.GetBlock(block.Id);
            AddOnPowerAxis(
                block.Id,
                centerX,
                y,
                measured,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            y +=
                measured.Size.Height +
                InternalVerticalGapMm;
        }

        double boardTop = y;
        double contentTop =
            boardTop + BoardHeaderHeightMm;

        MmSize peSize =
            measurement.GetBlock(peBus.Id).Size;
        MmSize neutralSize =
            measurement.GetBlock(neutralBus.Id).Size;

        Add(
            peBus.Id,
            new MmRect(
                branchAreaLeft,
                contentTop,
                peSize.Width,
                peSize.Height),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        Add(
            neutralBus.Id,
            new MmRect(
                boardLeft +
                boardWidth -
                BoardSideMarginMm -
                neutralSize.Width,
                contentTop,
                neutralSize.Width,
                neutralSize.Height),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double mainProtectionBottom = contentTop;
        foreach (CompositionBlock block in mainProtections)
        {
            MeasuredBlock measured =
                measurement.GetBlock(block.Id);

            AddOnPowerAxis(
                block.Id,
                centerX,
                mainProtectionBottom,
                measured,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            mainProtectionBottom +=
                measured.Size.Height +
                InternalVerticalGapMm;
        }

        double accessoriesBottom =
            Math.Max(
                contentTop + Math.Max(peSize.Height, neutralSize.Height),
                mainProtectionBottom);

        MeasuredBlock busMeasured =
            measurement.GetBlock(bus.Id);
        double busY =
            accessoriesBottom +
            InternalVerticalGapMm;
        double actualBranchAreaWidth =
            boardWidth - (BoardSideMarginMm * 2);

        Add(
            bus.Id,
            new MmRect(
                branchAreaLeft,
                busY,
                actualBranchAreaWidth,
                MainBusVisualHeightMm),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double branchY =
            busY +
            MainBusVisualHeightMm +
            InternalVerticalGapMm;

        for (int index = 0; index < columns.Length; index++)
        {
            BranchColumn column = columns[index];
            double slotCenterX =
                branchAreaLeft +
                (actualBranchAreaWidth *
                 (index + 0.5) /
                 columns.Length);

            // An odd branch count puts one circuit exactly on the incoming
            // feeder axis. Move only that colliding branch by the minimum
            // reviewed connection-node separation, preserving all other
            // established column geometry and board bounds.
            double minimumNodeSeparationMm =
                Math.Max(
                    2.4,
                    profile.GridMm);

            if (Math.Abs(slotCenterX - centerX) <
                0.000001)
            {
                slotCenterX +=
                    minimumNodeSeparationMm;
            }

            Add(
                column.Branch.Id,
                new MmRect(
                    slotCenterX - (SemanticBranchWidthMm / 2.0),
                    branchY,
                    SemanticBranchWidthMm,
                    SemanticBranchHeightMm),
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            double itemY =
                branchY +
                SemanticBranchHeightMm +
                InternalVerticalGapMm;

            foreach (CompositionBlock block in column.Children)
            {
                if (string.Equals(
                        block.SemanticRole,
                        "DownstreamBoard",
                        StringComparison.Ordinal))
                {
                    externalDestinations.Add(
                        (block, slotCenterX));
                    continue;
                }

                MeasuredBlock measured =
                    measurement.GetBlock(block.Id);

                AddOnPowerAxis(
                    block.Id,
                    slotCenterX,
                    itemY,
                    measured,
                    positioned,
                    assigned,
                    ref maxRight,
                    ref maxBottom);

                itemY +=
                    measured.Size.Height +
                    InternalVerticalGapMm;
            }
        }

        double boardBottom =
            positioned
                .Where(item =>
                    !incoming.Any(block =>
                        string.Equals(
                            block.Id,
                            item.BlockId,
                            StringComparison.Ordinal)))
                .Select(item => item.Bounds.Bottom)
                .DefaultIfEmpty(contentTop + 30)
                .Max() +
            BoardSideMarginMm;

        Add(
            boardFrame.Id,
            new MmRect(
                boardLeft,
                boardTop,
                boardWidth,
                Math.Max(
                    40,
                    boardBottom - boardTop)),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double externalDestinationY =
            boardBottom +
            InternalVerticalGapMm;

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
                externalDestinationY,
                measured,
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);
        }

        // Preserve explicit visibility of any future non-template extension
        // without allowing it to deform the deterministic RIC18 core.
        double auxiliaryY =
            maxBottom + profile.ContinuationRowGapMm;
        double auxiliaryX = boardLeft;

        foreach (CompositionBlock block in composition.Blocks
                     .Where(block => !assigned.Contains(block.Id))
                     .OrderBy(block => block.Id, StringComparer.Ordinal))
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
        CompositionBlock branch)
    {
        CompositionBlock[] children =
            composition.Blocks
                .Where(block =>
                    string.Equals(
                        block.ParentId,
                        branch.Id,
                        StringComparison.Ordinal))
                .OrderBy(ChildOrder)
                .ThenBy(block => block.Id, StringComparer.Ordinal)
                .ToArray();

        double requiredWidth =
            children.Length == 0
                ? MinimumSlotWidthMm
                : children
                    .Select(block =>
                        measurement.GetBlock(block.Id).Size.Width)
                    .Max();

        return new BranchColumn(
            branch,
            children,
            requiredWidth);
    }

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
                powerAxisX - axisOffset,
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
            .OrderBy(block => block.Id, StringComparer.Ordinal)
            .ToArray();

    private static CompositionBlock SingleByRole(
        DrawingComposition composition,
        string role)
    {
        CompositionBlock[] blocks =
            ByRole(composition, role);

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
        double RequiredWidthMm);
}
