using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class SummaryLayoutStrategyTests
{
    [Fact]
    public void Layout_PrimaryChain_IncreasesElectricalDepthWithoutOverlap()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Summary(
            SemanticFixtureFactory.NestedBoards(),
            profile);
        CompositionMeasurement measurement = Measure(composition, profile);

        PositionedLayout layout =
            new SummaryLayoutStrategy().Layout(
                composition,
                measurement,
                profile.Layout);

        MmRect source = layout.GetBlock("summary/source/S1").Bounds;
        MmRect main = layout.GetBlock("summary/board/B1").Bounds;
        MmRect downstream = layout.GetBlock("summary/board/B2").Bounds;

        Assert.True(main.X > source.X);
        Assert.True(downstream.X > main.X);
        AssertNoOverlaps(layout.Blocks);
    }

    [Fact]
    public void Layout_AlternateSource_DoesNotMovePrimaryBoardPositions()
    {
        RIC18DrawingProfile profile = Profile();

        DrawingComposition baseComposition = Summary(
            SemanticFixtureFactory.NestedBoards(),
            profile);
        PositionedLayout baseline = new SummaryLayoutStrategy().Layout(
            baseComposition,
            Measure(baseComposition, profile),
            profile.Layout);

        DrawingComposition alternateComposition = Summary(
            AddEmergencySupply(SemanticFixtureFactory.NestedBoards()),
            profile);
        PositionedLayout withAlternate = new SummaryLayoutStrategy().Layout(
            alternateComposition,
            Measure(alternateComposition, profile),
            profile.Layout);

        Assert.Equal(
            baseline.GetBlock("summary/board/B1").Bounds,
            withAlternate.GetBlock("summary/board/B1").Bounds);
        Assert.Equal(
            baseline.GetBlock("summary/board/B2").Bounds,
            withAlternate.GetBlock("summary/board/B2").Bounds);
    }

    [Fact]
    public void Layout_ShuffledComposition_IsDeterministic()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition source = Summary(
            SemanticFixtureFactory.NestedBoards(),
            profile);
        var shuffled = new DrawingComposition(
            source.Kind,
            source.Scope,
            source.Blocks.Reverse(),
            source.Connections.Reverse(),
            source.InputFingerprint,
            source.ProfileFingerprint);

        PositionedLayout first = new SummaryLayoutStrategy().Layout(
            source,
            Measure(source, profile),
            profile.Layout);
        PositionedLayout second = new SummaryLayoutStrategy().Layout(
            shuffled,
            Measure(shuffled, profile),
            profile.Layout);

        Assert.Equal(
            first.Blocks.Select(x => (x.BlockId, x.Bounds)),
            second.Blocks.Select(x => (x.BlockId, x.Bounds)));
        Assert.Equal(first.Bounds, second.Bounds);
    }

    [Fact]
    public void Layout_SameDepthBlocks_ArePlacedInStableRows()
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineInput input = AddEmergencySupply(
            SemanticFixtureFactory.NestedBoards());
        DrawingComposition composition = Summary(input, profile);

        PositionedLayout layout = new SummaryLayoutStrategy().Layout(
            composition,
            Measure(composition, profile),
            profile.Layout);

        MmRect utility = layout.GetBlock("summary/source/S1").Bounds;
        MmRect emergency = layout.GetBlock("summary/source/S2").Bounds;

        Assert.Equal(utility.X, emergency.X);
        Assert.NotEqual(utility.Y, emergency.Y);
        AssertNoOverlaps(layout.Blocks);
    }

    private static DrawingComposition Summary(
        SingleLineInput input,
        RIC18DrawingProfile profile)
    {
        ProjectionBuildResult result =
            new SingleLineProjectionBuilder().Build(input);
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Validation.Issues));
        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(result.Projection);

        return new CompositionBuilder().BuildSummary(projection, profile);
    }

    private static CompositionMeasurement Measure(
        DrawingComposition composition,
        RIC18DrawingProfile profile) =>
        new CompositionMeasurer(new DeterministicTextMetrics())
            .Measure(composition, profile);

    private static SingleLineInput AddEmergencySupply(SingleLineInput source)
    {
        var emergency = new SourceInput(
            new EntityUid("S2"),
            "GEN",
            "Generador",
            SourceKind.Generator,
            "3F",
            400m,
            3,
            true,
            OperationalState.Active,
            DataState.Complete);
        var supply = new SupplyConnection(
            new EntityUid("SC-ALT"),
            new EntityReference(emergency.Uid, EntityKind.Source),
            null,
            new EntityUid("B1"),
            SupplyRole.Emergency,
            1,
            false,
            OperationalState.Active,
            DataState.Complete);

        return new SingleLineInput(
            source.Project,
            [.. source.Sources, emergency],
            source.Boards,
            source.Buses,
            source.Circuits,
            [.. source.SupplyConnections, supply],
            source.Protections,
            source.Grounding,
            source.Results,
            source.Metadata);
    }

    private static void AssertNoOverlaps(
        IReadOnlyList<PositionedCompositionBlock> blocks)
    {
        for (int left = 0; left < blocks.Count; left++)
        {
            for (int right = left + 1; right < blocks.Count; right++)
            {
                Assert.False(
                    Overlaps(blocks[left].Bounds, blocks[right].Bounds),
                    $"Overlap: {blocks[left].BlockId} / {blocks[right].BlockId}");
            }
        }
    }

    private static bool Overlaps(MmRect a, MmRect b) =>
        a.X < b.Right &&
        a.Right > b.X &&
        a.Y < b.Bottom &&
        a.Bottom > b.Y;

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
