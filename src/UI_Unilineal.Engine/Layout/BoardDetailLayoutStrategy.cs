using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class BoardDetailLayoutStrategy : ISingleLineLayoutStrategy
{
    private const double BoardSideMarginMm = 8;
    private const double BoardTopMarginMm = 6;

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

        double maxRight = 0;
        double maxBottom = 0;

        BranchColumn[] columns =
            branches
                .Select(branch =>
                    MeasureColumn(
                        composition,
                        measurement,
                        profile,
                        branch))
                .ToArray();

        double branchSpan =
            columns.Length == 0
                ? measurement.GetBlock(bus.Id).Size.Width
                : columns.Sum(column => column.Width) +
                  (profile.BranchGapMm *
                   Math.Max(0, columns.Length - 1));

        double boardWidth =
            Math.Max(
                80,
                branchSpan + (BoardSideMarginMm * 2));

        double boardLeft = profile.GridMm;
        double centerX =
            boardLeft + (boardWidth / 2.0);

        // Incoming supply / EMPALME is always outside and above the board.
        double y = profile.GridMm;
        foreach (CompositionBlock block in incoming)
        {
            MmSize size =
                measurement.GetBlock(block.Id).Size;
            Add(
                block.Id,
                new MmRect(
                    centerX - (size.Width / 2.0),
                    y,
                    size.Width,
                    size.Height),
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);
            y += size.Height + profile.VerticalGapMm;
        }

        // Everything below this point belongs to the board visual template.
        double boardTop =
            y + BoardTopMarginMm;

        MmSize peSize =
            measurement.GetBlock(peBus.Id).Size;
        MmSize neutralSize =
            measurement.GetBlock(neutralBus.Id).Size;

        double accessoryY = boardTop;

        Add(
            peBus.Id,
            new MmRect(
                boardLeft + BoardSideMarginMm,
                accessoryY,
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
                accessoryY,
                neutralSize.Width,
                neutralSize.Height),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double protectionY = accessoryY;

        foreach (CompositionBlock block in mainProtections)
        {
            MmSize size =
                measurement.GetBlock(block.Id).Size;

            Add(
                block.Id,
                new MmRect(
                    centerX - (size.Width / 2.0),
                    protectionY,
                    size.Width,
                    size.Height),
                positioned,
                assigned,
                ref maxRight,
                ref maxBottom);

            protectionY +=
                size.Height +
                profile.VerticalGapMm;
        }

        double accessoryBottom =
            Math.Max(
                accessoryY + Math.Max(peSize.Height, neutralSize.Height),
                protectionY);

        MmSize busSize =
            measurement.GetBlock(bus.Id).Size;
        double busY =
            accessoryBottom +
            profile.VerticalGapMm;

        Add(
            bus.Id,
            new MmRect(
                boardLeft + BoardSideMarginMm,
                busY,
                boardWidth - (BoardSideMarginMm * 2),
                busSize.Height),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double branchY =
            busY +
            busSize.Height +
            profile.VerticalGapMm;
        double branchX =
            boardLeft + BoardSideMarginMm;

        foreach (BranchColumn column in columns)
        {
            double itemY = branchY;

            foreach (CompositionBlock block in column.Blocks)
            {
                MmSize size =
                    measurement.GetBlock(block.Id).Size;

                double x =
                    branchX +
                    ((column.Width - size.Width) / 2.0);

                Add(
                    block.Id,
                    new MmRect(
                        x,
                        itemY,
                        size.Width,
                        size.Height),
                    positioned,
                    assigned,
                    ref maxRight,
                    ref maxBottom);

                itemY +=
                    size.Height +
                    profile.VerticalGapMm;
            }

            branchX +=
                column.Width +
                profile.BranchGapMm;
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
                .DefaultIfEmpty(boardTop + 40)
                .Max();

        Add(
            boardFrame.Id,
            new MmRect(
                boardLeft,
                boardTop - 2,
                boardWidth,
                Math.Max(
                    40,
                    boardBottom - boardTop + BoardSideMarginMm + 2)),
            positioned,
            assigned,
            ref maxRight,
            ref maxBottom);

        double auxiliaryY =
            Math.Max(
                maxBottom + profile.ContinuationRowGapMm,
                branchY + profile.ContinuationRowGapMm);
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
        LayoutProfile profile,
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

        CompositionBlock[] column =
            [branch, .. children];

        double width =
            column
                .Select(block =>
                    measurement.GetBlock(block.Id).Size.Width)
                .Max();
        double height =
            column
                .Select(block =>
                    measurement.GetBlock(block.Id).Size.Height)
                .Sum() +
            (profile.VerticalGapMm *
             Math.Max(0, column.Length - 1));

        return new BranchColumn(
            branch.Id,
            column,
            width,
            height);
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
        string BranchId,
        IReadOnlyList<CompositionBlock> Blocks,
        double Width,
        double Height);
}
