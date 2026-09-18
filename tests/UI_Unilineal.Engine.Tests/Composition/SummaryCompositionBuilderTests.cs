using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class SummaryCompositionBuilderTests
{
    [Fact]
    public void BuildSummary_MinimalProjection_ProducesSourceBoardAndPowerConnection()
    {
        SingleLineProjection projection = Project(SemanticFixtureFactory.Minimal());
        RIC18DrawingProfile profile = Profile();

        DrawingComposition composition =
            new CompositionBuilder().BuildSummary(projection, profile);

        Assert.Equal(DrawingCompositionKind.Summary, composition.Kind);
        Assert.Equal(2, composition.Blocks.Count);
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "SOURCE_BLOCK" &&
                 x.Id == "summary/source/S1");
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "BOARD_SUMMARY_BLOCK" &&
                 x.Id == "summary/board/B1");

        CompositionConnection connection = Assert.Single(composition.Connections);
        Assert.Equal("summary/source/S1", connection.Source.BlockId);
        Assert.Equal("summary/board/B1", connection.Target.BlockId);
        Assert.Equal("POWER", connection.LineStyleId);
    }

    [Fact]
    public void BuildSummary_AlternateSupply_AddsConnectionWithoutDuplicatingBoard()
    {
        SingleLineProjection projection = Project(MultipleSourceInput());

        DrawingComposition composition =
            new CompositionBuilder().BuildSummary(projection, Profile());

        Assert.Equal(3, composition.Blocks.Count);
        Assert.Equal(
            1,
            composition.Blocks.Count(x =>
                x.BlockDefinitionId == "BOARD_SUMMARY_BLOCK"));
        Assert.Equal(2, composition.Connections.Count);
        Assert.Contains(
            composition.Connections,
            x => x.LineStyleId == "ALTERNATE_SUPPLY");
    }

    [Fact]
    public void BuildSummary_MissingConnectionEndpoint_UsesUnknownBlock()
    {
        var boardEntity = new EntityReference(
            new EntityUid("B1"),
            EntityKind.Board);
        var missing = new EntityReference(
            new EntityUid("UNKNOWN-ENDPOINT"),
            EntityKind.Load);

        var summary = new SummaryProjection(
            [
                new SummaryNode(
                    boardEntity,
                    "TGBT",
                    "Tablero",
                    BoardRole.Main,
                    ProjectionStatus.Warning,
                    0,
                    1)
            ],
            [
                new SummaryConnection(
                    new EntityUid("SC-X"),
                    missing,
                    null,
                    boardEntity,
                    SupplyRole.Normal,
                    true,
                    ProjectionStatus.Warning)
            ],
            [missing],
            []);
        var projection = new SingleLineProjection(
            new EntityUid("P1"),
            summary,
            [],
            [],
            "INPUT");

        DrawingComposition composition =
            new CompositionBuilder().BuildSummary(projection, Profile());

        CompositionBlock unknown = Assert.Single(
            composition.Blocks,
            x => x.BlockDefinitionId == "UNKNOWN_BLOCK");
        Assert.Equal(
            CompositionIdFactory.SummaryEntity(missing),
            unknown.Id);
        Assert.Equal(2, composition.Blocks.Count);
        Assert.Single(composition.Connections);
    }

    private static SingleLineProjection Project(SingleLineInput input)
    {
        ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(input);
        Assert.True(result.Success);
        return Assert.IsType<SingleLineProjection>(result.Projection);
    }

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));

    private static SingleLineInput MultipleSourceInput()
    {
        SingleLineInput minimal = SemanticFixtureFactory.Minimal();
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
        var emergencySupply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(emergency.Uid, EntityKind.Source),
            null,
            minimal.Boards[0].Uid,
            SupplyRole.Emergency,
            1,
            false,
            OperationalState.Active,
            DataState.Complete);

        return new SingleLineInput(
            minimal.Project,
            [minimal.Sources[0], emergency],
            minimal.Boards,
            minimal.Buses,
            minimal.Circuits,
            [minimal.SupplyConnections[0], emergencySupply],
            minimal.Protections,
            minimal.Grounding,
            minimal.Results,
            minimal.Metadata);
    }
}
