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

        Assert.Equal(
            [new SceneId("detail/B1/branch/C1/axis")],
            branch.ChildIds);
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
            pe.Bounds.Y >= frame.Bounds.Y + 14);
        Assert.True(
            neutral.Bounds.Y >= frame.Bounds.Y + 14);
        Assert.True(pe.Bounds.X < neutral.Bounds.X);
    }

    [Fact]
    public void MinimalBoard_CircuitBranchContainsContinuousPowerSegment()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement branch =
            Group(
                scene,
                "detail/B1/branch/C1");
        LineSceneElement axis =
            Assert.IsType<LineSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value ==
                    "detail/B1/branch/C1/axis"));

        Assert.Equal(
            Anchor(branch, "IN").Point,
            axis.Start);
        Assert.Equal(
            Anchor(branch, "OUT").Point,
            axis.End);
    }

    [Fact]
    public void MinimalBoard_FinalLoadIsExternalDestinationOfItsCircuit()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement frame =
            Group(
                scene,
                "detail/B1");
        GroupSceneElement branch =
            Group(
                scene,
                "detail/B1/branch/C1");
        GroupSceneElement protection =
            Group(
                scene,
                "detail/B1/branch/C1/protection/PR1");
        GroupSceneElement destination =
            Group(
                scene,
                "detail/B1/branch/C1/destination");

        Assert.True(
            protection.Bounds.Bottom <=
            frame.Bounds.Bottom);
        Assert.True(
            destination.Bounds.Y >=
            frame.Bounds.Bottom);
        Assert.Equal(
            Anchor(branch, "IN").Point.X,
            Anchor(destination, "IN").Point.X);
    }

    [Fact]
    public void DifferentialBranch_NeutralPassesThroughRcdAndTpBypassesIt()
    {
        DiagramScene scene =
            BuildScene(
                MinimalWithDifferential(),
                new EntityUid("B1"));
        GroupSceneElement rcd =
            Group(
                scene,
                "detail/B1/branch/C1/protection/PR-RCD");
        GroupSceneElement destination =
            Group(
                scene,
                "detail/B1/branch/C1/destination");

        PolylineSceneElement neutralIn =
            Route(
                scene,
                "detail/B1/connection/neutral-in/C1");
        PolylineSceneElement neutralOut =
            Route(
                scene,
                "detail/B1/connection/neutral-out/C1");
        PolylineSceneElement protectiveEarth =
            Route(
                scene,
                "detail/B1/connection/protective-earth/C1");

        Assert.Equal(
            Anchor(rcd, "N_IN").Point,
            neutralIn.Points[^1]);
        Assert.Equal(
            Anchor(rcd, "N_OUT").Point,
            neutralOut.Points[0]);
        Assert.Equal(
            Anchor(destination, "N").Point,
            neutralOut.Points[^1]);
        Assert.Equal(
            Anchor(destination, "PE").Point,
            protectiveEarth.Points[^1]);
        Assert.DoesNotContain(
            protectiveEarth.Points,
            point =>
                point == Anchor(rcd, "N_IN").Point ||
                point == Anchor(rcd, "N_OUT").Point);
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

    [Fact]
    public void MinimalBoard_Ric18BusGrammar_RequiresDistinctPowerNodesAndCompactBus()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement bus =
            Group(scene, "detail/B1/bus/BUS:B1:MAIN");
        SceneAnchor incoming =
            Anchor(bus, "IN");
        SceneAnchor branch =
            Anchor(bus, "TAP:C1");
        LineSceneElement rail =
            Assert.IsType<LineSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value ==
                    "detail/B1/bus/BUS:B1:MAIN/rail"));

        const double StandardIncomingToBranchPitchMm = 17.0;

        Assert.NotEqual(incoming.Point.X, branch.Point.X);
        Assert.True(
            Math.Abs(incoming.Point.X - branch.Point.X) >=
            StandardIncomingToBranchPitchMm);
        Assert.True(rail.Start.X <= Math.Min(incoming.Point.X, branch.Point.X));
        Assert.True(rail.End.X >= Math.Max(incoming.Point.X, branch.Point.X));
        Assert.True(rail.End.X - rail.Start.X <=
                    Math.Abs(incoming.Point.X - branch.Point.X) + 2.4 + 0.000001);

        CircleSceneElement[] connectionNodes =
            scene.Elements
                .OfType<CircleSceneElement>()
                .Where(element =>
                    element.Id.Value.StartsWith(
                        "detail/B1/bus/BUS:B1:MAIN/node/",
                        StringComparison.Ordinal))
                .ToArray();

        Assert.Equal(2, connectionNodes.Length);
    }

    [Fact]
    public void MinimalBoard_Ric18AuxiliaryRoutes_LeaveHeaderRailsDownward()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));

        foreach (string id in new[]
                 {
                     "detail/B1/connection/neutral/C1",
                     "detail/B1/connection/protective-earth/C1"
                 })
        {
            PolylineSceneElement route =
                Route(scene, id);

            Assert.True(route.Points.Count >= 3);
            Assert.Equal(route.Points[0].X, route.Points[1].X);
            Assert.True(route.Points[1].Y > route.Points[0].Y);
        }
    }

    [Fact]
    public void SyntheticConnectionNodes_AreMarkedForSolidFill()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));

        CircleSceneElement[] nodes =
            scene.Elements
                .OfType<CircleSceneElement>()
                .Where(circle =>
                    circle.Id.Value.Contains(
                        "/node/",
                        StringComparison.Ordinal) ||
                    circle.Id.Value.Contains(
                        "/terminal/",
                        StringComparison.Ordinal))
                .ToArray();

        Assert.NotEmpty(
            nodes);

        Assert.All(
            nodes,
            node =>
            {
                Assert.True(
                    node.Metadata.TryGetValue(
                        "fillMode",
                        out string? fillMode));
                Assert.Equal(
                    "Solid",
                    fillMode);
            });
    }

    [Fact]
    public void DestinationAuxiliaryAnchors_AreBackedByVisibleTerminalNodes()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement destination =
            Group(
                scene,
                "detail/B1/branch/C1/destination");

        foreach (string anchorId in new[] { "N", "PE" })
        {
            SceneAnchor anchor =
                Anchor(
                    destination,
                    anchorId);

            Assert.Contains(
                scene.Elements.OfType<CircleSceneElement>(),
                circle =>
                    circle.Id.Value.StartsWith(
                        $"{destination.Id.Value}/terminal/",
                        StringComparison.Ordinal) &&
                    circle.Center == anchor.Point);
        }
    }

    [Fact]
    public void DownstreamBoard_IsExternalDestinationOfItsCircuit()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.NestedBoards(),
                new EntityUid("B1"));

        GroupSceneElement board =
            Group(scene, "detail/B1");
        GroupSceneElement protection =
            Group(scene, "detail/B1/branch/C4/protection/PR4");
        GroupSceneElement destination =
            Group(scene, "detail/B1/branch/C4/destination");
        GroupSceneElement branch =
            Group(scene, "detail/B1/branch/C4");

        Assert.True(protection.Bounds.Bottom <= board.Bounds.Bottom);
        Assert.True(destination.Bounds.Y >= board.Bounds.Bottom);
        Assert.Equal(
            Anchor(branch, "IN").Point.X,
            Anchor(destination, "IN").Point.X);
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

    private static PolylineSceneElement Route(
        DiagramScene scene,
        string connectionId) =>
        Assert.IsType<PolylineSceneElement>(
            scene.Elements.Single(element =>
                element is PolylineSceneElement route &&
                route.Metadata.TryGetValue(
                    "connectionId",
                    out string? value) &&
                value == connectionId));

    private static SingleLineInput MinimalWithDifferential()
    {
        SingleLineInput source =
            SemanticFixtureFactory.Minimal();
        CircuitInput circuit =
            Assert.Single(source.Circuits);
        var differential =
            new ProtectionInput(
                new EntityUid("PR-RCD"),
                new EntityReference(
                    circuit.Uid,
                    EntityKind.Circuit),
                new EntityReference(
                    circuit.Uid,
                    EntityKind.Circuit),
                ProtectionKind.Differential,
                ProtectionRole.Adopted,
                2,
                25m,
                null,
                null,
                30m,
                "A",
                null,
                null,
                OperationalState.Active,
                DataState.Complete);

        return new SingleLineInput(
            source.Project,
            source.Sources,
            source.Boards,
            source.Buses,
            source.Circuits,
            source.SupplyConnections,
            [.. source.Protections, differential],
            source.Grounding,
            source.Results,
            source.Metadata,
            source.ServiceEntrances);
    }

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(
                AppContext.BaseDirectory,
                "ProfileData"));
}
