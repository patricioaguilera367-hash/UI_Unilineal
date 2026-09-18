using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Scene;

public sealed class SceneAssemblyTests
{
    [Fact]
    public void Assemble_PositionedBreakerBlock_ExpandsSymbolTextAndAnchors()
    {
        RIC18DrawingProfile profile = Profile();
        CompositionBlock block = BreakerBlock();
        DrawingComposition composition = Composition(profile, block);
        var input = new SceneAssemblyInput(
            composition,
            [
                new PositionedCompositionBlock(
                    block.Id,
                    new MmRect(10, 10, 20, 24))
            ],
            new MmRect(0, 0, 100, 100),
            "PROJECTION",
            "G4-TEST");

        DiagramScene scene = new SceneAssembly().Assemble(input, profile);

        GroupSceneElement group = Assert.IsType<GroupSceneElement>(
            Assert.Single(scene.Elements, x => x.Id == new SceneId(block.Id)));
        Assert.Contains(group.Anchors, x => x.Id == "IN");
        Assert.Contains(group.Anchors, x => x.Id == "OUT");

        SymbolSceneElement symbol = Assert.Single(
            scene.Elements.OfType<SymbolSceneElement>());
        Assert.Equal("BREAKER", symbol.SymbolDefinitionId);

        TextSceneElement text = Assert.Single(
            scene.Elements.OfType<TextSceneElement>());
        Assert.Equal("10 A", text.Text);
        Assert.Equal("LABEL_SMALL", text.TextStyleId);

        Assert.Equal(
            3,
            scene.Elements.OfType<LineSceneElement>().Count());
    }

    [Fact]
    public void Assemble_SamePositionedComposition_HasStableSceneFingerprint()
    {
        RIC18DrawingProfile profile = Profile();
        CompositionBlock block = BreakerBlock();
        DrawingComposition composition = Composition(profile, block);
        var input = new SceneAssemblyInput(
            composition,
            [
                new PositionedCompositionBlock(
                    block.Id,
                    new MmRect(10, 10, 20, 24))
            ],
            new MmRect(0, 0, 100, 100),
            "PROJECTION",
            "G4-TEST");

        DiagramScene first = new SceneAssembly().Assemble(input, profile);
        DiagramScene second = new SceneAssembly().Assemble(input, profile);

        Assert.Equal(
            DiagramSceneFingerprint.Compute(first),
            DiagramSceneFingerprint.Compute(second));
    }

    [Fact]
    public void Assemble_MissingBlockPosition_ThrowsInsteadOfChoosingCoordinates()
    {
        RIC18DrawingProfile profile = Profile();
        CompositionBlock block = BreakerBlock();
        DrawingComposition composition = Composition(profile, block);
        var input = new SceneAssemblyInput(
            composition,
            [],
            new MmRect(0, 0, 100, 100),
            "PROJECTION",
            "G4-TEST");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new SceneAssembly().Assemble(input, profile));

        Assert.Contains(block.Id, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_MinimalSummary_ProducesStructurallyValidSceneConnections()
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineInput semantic = SemanticFixtureFactory.Minimal();
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(semantic);
        Assert.True(projectionResult.Success);
        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(projectionResult.Projection);
        DrawingComposition composition =
            new CompositionBuilder().BuildSummary(projection, profile);

        var input = new SceneAssemblyInput(
            composition,
            [
                new PositionedCompositionBlock(
                    "summary/source/S1",
                    new MmRect(10, 10, 24, 24)),
                new PositionedCompositionBlock(
                    "summary/board/B1",
                    new MmRect(50, 10, 30, 24))
            ],
            new MmRect(0, 0, 100, 60),
            projection.InputFingerprint,
            "G4-TEST");

        DiagramScene scene = new SceneAssembly().Assemble(input, profile);
        DiagramSceneValidationResult validation =
            new DiagramSceneValidator().Validate(scene);

        Assert.False(
            validation.HasErrors,
            string.Join(Environment.NewLine, validation.Issues));
        Assert.Single(scene.Connections);
        Assert.Equal(
            new SceneId("summary/source/S1"),
            scene.Connections[0].Source.ElementId);
        Assert.Equal(
            new SceneId("summary/board/B1"),
            scene.Connections[0].Target.ElementId);
    }

    private static CompositionBlock BreakerBlock() =>
        new(
            "detail/B1/branch/C1/protection/PR1",
            "PROTECTION_CHAIN_BLOCK",
            "Protection",
            new EntityReference(new EntityUid("PR1"), EntityKind.Protection),
            new Dictionary<string, string>
            {
                ["RATING"] = "10 A"
            },
            ProjectionStatus.Ok,
            "detail/B1/branch/C1",
            new Dictionary<string, string>
            {
                ["PROTECTION"] = "BREAKER"
            });

    private static DrawingComposition Composition(
        RIC18DrawingProfile profile,
        CompositionBlock block) =>
        new(
            DrawingCompositionKind.BoardDetail,
            new EntityReference(new EntityUid("B1"), EntityKind.Board),
            [block],
            [],
            "INPUT",
            DrawingProfileFingerprint.Compute(profile));

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
