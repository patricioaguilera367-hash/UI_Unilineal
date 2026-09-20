using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class BoardDetailLayoutStrategyTests
{
    [Fact]
    public void Layout_MainPathIsVerticalAndBranchesStartBelowBus()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, 3);
        CompositionMeasurement measurement = Measure(composition, profile);

        PositionedLayout layout =
            new BoardDetailLayoutStrategy().Layout(
                composition,
                measurement,
                profile.Layout);

        MmRect incoming = layout.GetBlock("detail/B1/incoming/SC1").Bounds;
        MmRect mainProtection =
            layout.GetBlock("detail/B1/main-protection/PM").Bounds;
        MmRect bus = layout.GetBlock("detail/B1/bus/BUS:B1:MAIN").Bounds;

        Assert.True(mainProtection.Y > incoming.Bottom);
        Assert.True(bus.Y > mainProtection.Bottom);

        foreach (int index in Enumerable.Range(1, 3))
        {
            MmRect branch = layout
                .GetBlock($"detail/B1/branch/C{index:D3}")
                .Bounds;
            Assert.True(branch.Y > bus.Bottom);
        }

        AssertNoOverlaps(layout.Blocks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(100)]
    public void Layout_ManyBranches_HaveNoStructuralBlockOverlap(int branchCount)
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, branchCount);

        PositionedLayout layout =
            new BoardDetailLayoutStrategy().Layout(
                composition,
                Measure(composition, profile),
                profile.Layout);

        Assert.Equal(
            6 + (branchCount * 3),
            layout.Blocks.Count);
        AssertNoOverlaps(layout.Blocks);

        Assert.All(
            layout.Blocks,
            block =>
            {
                Assert.True(double.IsFinite(block.Bounds.X));
                Assert.True(double.IsFinite(block.Bounds.Y));
                Assert.True(block.Bounds.Width > 0);
                Assert.True(block.Bounds.Height > 0);
                Assert.True(Contains(layout.Bounds, block.Bounds));
            });
    }

    [Fact]
    public void Layout_LargeBoard_KeepsBranchesOnOneRic18RowAndStretchesBus()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, 24);

        PositionedLayout layout =
            new BoardDetailLayoutStrategy().Layout(
                composition,
                Measure(composition, profile),
                profile.Layout);

        MmRect[] branches = Enumerable.Range(1, 24)
            .Select(index => layout
                .GetBlock($"detail/B1/branch/C{index:D3}")
                .Bounds)
            .ToArray();
        MmRect bus =
            layout.GetBlock("detail/B1/bus/BUS:B1:MAIN").Bounds;

        Assert.Single(
            branches.Select(x => x.Y).Distinct());
        Assert.True(
            bus.Width >=
            branches.Max(branch => branch.Right) -
            branches.Min(branch => branch.X));
    }

    [Fact]
    public void Layout_ShuffledComposition_IsDeterministic()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition source = Composition(profile, 12);
        var shuffled = new DrawingComposition(
            source.Kind,
            source.Scope,
            source.Blocks.Reverse(),
            source.Connections.Reverse(),
            source.InputFingerprint,
            source.ProfileFingerprint);

        PositionedLayout first = new BoardDetailLayoutStrategy().Layout(
            source,
            Measure(source, profile),
            profile.Layout);
        PositionedLayout second = new BoardDetailLayoutStrategy().Layout(
            shuffled,
            Measure(shuffled, profile),
            profile.Layout);

        Assert.Equal(
            first.Blocks.Select(x => (x.BlockId, x.Bounds)),
            second.Blocks.Select(x => (x.BlockId, x.Bounds)));
        Assert.Equal(first.Bounds, second.Bounds);
    }

    private static DrawingComposition Composition(
        RIC18DrawingProfile profile,
        int branchCount)
    {
        var blocks = new List<CompositionBlock>
        {
            Block(
                "detail/B1",
                "BOARD_DETAIL_FRAME_BLOCK",
                "BoardFrame",
                null,
                new Dictionary<string, string>
                {
                    ["CODE"] = "TGBT",
                    ["NAME"] = "Tablero general"
                }),
            Block(
                "detail/B1/incoming/SC1",
                "INCOMING_SUPPLY_BLOCK",
                "IncomingSupply",
                null,
                new Dictionary<string, string>
                {
                    ["CODE"] = "EMPALME"
                }),
            Block(
                "detail/B1/main-protection/PM",
                "MAIN_PROTECTION_BLOCK",
                "MainProtection",
                null,
                new Dictionary<string, string>
                {
                    ["RATING"] = "100 A"
                }),
            Block(
                "detail/B1/bus/BUS:B1:MAIN",
                "MAIN_BUS_BLOCK",
                "MainBus",
                null,
                new Dictionary<string, string>
                {
                    ["CODE"] = "MAIN"
                }),
            Block(
                "detail/B1/bus/NEUTRAL",
                "NEUTRAL_BUS_BLOCK",
                "NeutralBus",
                null,
                new Dictionary<string, string>
                {
                    ["LABEL"] = "N"
                }),
            Block(
                "detail/B1/bus/PE",
                "PE_BUS_BLOCK",
                "ProtectiveEarthBus",
                null,
                new Dictionary<string, string>
                {
                    ["LABEL"] = "TP"
                })
        };

        for (int index = 1; index <= branchCount; index++)
        {
            string circuit = $"C{index:D3}";
            string branchId = $"detail/B1/branch/{circuit}";

            blocks.Add(Block(
                branchId,
                "CIRCUIT_BRANCH_BLOCK",
                "CircuitBranch",
                "detail/B1/bus/BUS:B1:MAIN",
                new Dictionary<string, string>
                {
                    ["NAME"] = circuit
                }));
            blocks.Add(new CompositionBlock(
                $"{branchId}/protection/P{index:D3}",
                "PROTECTION_CHAIN_BLOCK",
                "Protection",
                null,
                new Dictionary<string, string>
                {
                    ["RATING"] = $"{10 + index} A"
                },
                ProjectionStatus.Ok,
                branchId,
                new Dictionary<string, string>
                {
                    ["PROTECTION"] = "BREAKER"
                }));
            blocks.Add(Block(
                $"{branchId}/destination",
                "FINAL_LOAD_BLOCK",
                "FinalLoad",
                branchId,
                new Dictionary<string, string>
                {
                    ["NAME"] = $"Carga {index:D3}"
                }));
        }

        return new DrawingComposition(
            DrawingCompositionKind.BoardDetail,
            null,
            blocks,
            [],
            "INPUT",
            DrawingProfileFingerprint.Compute(profile));
    }

    private static CompositionBlock Block(
        string id,
        string definition,
        string role,
        string? parentId,
        IReadOnlyDictionary<string, string> labels) =>
        new(
            id,
            definition,
            role,
            null,
            labels,
            ProjectionStatus.Ok,
            parentId);

    private static CompositionMeasurement Measure(
        DrawingComposition composition,
        RIC18DrawingProfile profile) =>
        new CompositionMeasurer(new DeterministicTextMetrics())
            .Measure(composition, profile);

    private static void AssertNoOverlaps(
        IReadOnlyList<PositionedCompositionBlock> blocks)
    {
        PositionedCompositionBlock[] structural =
            blocks
                .Where(block =>
                    !string.Equals(
                        block.BlockId,
                        "detail/B1",
                        StringComparison.Ordinal))
                .ToArray();

        for (int left = 0; left < structural.Length; left++)
        {
            for (int right = left + 1; right < structural.Length; right++)
            {
                Assert.False(
                    Overlaps(
                        structural[left].Bounds,
                        structural[right].Bounds),
                    $"Overlap: {structural[left].BlockId} / {structural[right].BlockId}");
            }
        }
    }

    private static bool Overlaps(MmRect a, MmRect b) =>
        a.X < b.Right &&
        a.Right > b.X &&
        a.Y < b.Bottom &&
        a.Bottom > b.Y;

    private static bool Contains(MmRect outer, MmRect inner) =>
        inner.X >= outer.X &&
        inner.Y >= outer.Y &&
        inner.Right <= outer.Right &&
        inner.Bottom <= outer.Bottom;

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
