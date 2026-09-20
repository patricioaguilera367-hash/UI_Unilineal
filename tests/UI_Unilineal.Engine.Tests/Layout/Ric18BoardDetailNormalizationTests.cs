using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class Ric18BoardDetailNormalizationTests
{
    [Theory]
    [InlineData("minimal")]
    [InlineData("nested")]
    public void BoardDetail_CorePowerRoutesStayInsideBoardAndDoNotDetourOutside(
        string fixture)
    {
        SingleLineInput input =
            fixture == "minimal"
                ? SemanticFixtureFactory.Minimal()
                : SemanticFixtureFactory.NestedBoards();
        EntityUid boardUid =
            new("B1");
        DiagramScene scene =
            BuildScene(
                input,
                boardUid);

        GroupSceneElement frame =
            Assert.IsType<GroupSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value == "detail/B1"));

        PolylineSceneElement[] powerRoutes =
            scene.Elements
                .OfType<PolylineSceneElement>()
                .Where(route =>
                    route.Metadata.ContainsKey("connectionId") &&
                    route.LineStyleId == "POWER")
                .ToArray();

        Assert.NotEmpty(powerRoutes);
        Assert.All(
            powerRoutes,
            route => Assert.All(
                route.Points,
                point => Assert.InRange(
                    point.X,
                    frame.Bounds.X,
                    frame.Bounds.Right)));
    }

    [Fact]
    public void MinimalBoard_BusBranchProtectionAndDestinationSharePowerAxis()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));

        GroupSceneElement bus =
            Group(scene, "detail/B1/bus/BUS:B1:MAIN");
        GroupSceneElement branch =
            Group(scene, "detail/B1/branch/C1");
        GroupSceneElement protection =
            Group(scene, "detail/B1/branch/C1/protection/PR1");
        GroupSceneElement destination =
            Group(scene, "detail/B1/branch/C1/destination");

        double axis =
            Anchor(bus, "TAP:C1").Point.X;

        Assert.Equal(
            axis,
            Anchor(branch, "IN").Point.X);
        Assert.Equal(
            axis,
            Anchor(branch, "OUT").Point.X);
        Assert.Equal(
            axis,
            Anchor(protection, "IN").Point.X);
        Assert.Equal(
            axis,
            Anchor(protection, "OUT").Point.X);
        Assert.Equal(
            axis,
            Anchor(destination, "IN").Point.X);

        Assert.Empty(branch.ChildIds);
    }

    [Fact]
    public void MinimalBoard_HeaderBarsDoNotCollideWithBoardIdentity()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));

        GroupSceneElement frame =
            Group(scene, "detail/B1");
        GroupSceneElement pe =
            Group(scene, "detail/B1/bus/PE");
        GroupSceneElement neutral =
            Group(scene, "detail/B1/bus/NEUTRAL");

        Assert.True(
            pe.Bounds.Y >= frame.Bounds.Y + 10);
        Assert.True(
            neutral.Bounds.Y >= frame.Bounds.Y + 10);
        Assert.True(pe.Bounds.X < neutral.Bounds.X);
    }

    [Fact]
    public void DestinationAuxiliaryAnchorsKeepTpLeftAndNeutralRight()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));

        GroupSceneElement destination =
            Group(scene, "detail/B1/branch/C1/destination");

        Assert.True(
            Anchor(destination, "PE").Point.X <
            Anchor(destination, "N").Point.X);
    }

    private static DiagramScene BuildScene(
        SingleLineInput input,
        EntityUid boardUid)
    {
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(input);
        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(
                projectionResult.Projection);
        RIC18DrawingProfile profile =
            Profile();

        SingleLineLayoutResult result =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics())
                .LayoutBoardDetail(
                    projection,
                    boardUid,
                    profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);

        return Assert.IsType<DiagramScene>(
            result.Scene);
    }

    private static GroupSceneElement Group(
        DiagramScene scene,
        string id) =>
        Assert.IsType<GroupSceneElement>(
            scene.Elements.Single(element =>
                element.Id.Value == id));

    private static SceneAnchor Anchor(
        GroupSceneElement group,
        string id) =>
        group.Anchors.Single(anchor =>
            anchor.Id == id);

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(
                AppContext.BaseDirectory,
                "ProfileData"));
}
