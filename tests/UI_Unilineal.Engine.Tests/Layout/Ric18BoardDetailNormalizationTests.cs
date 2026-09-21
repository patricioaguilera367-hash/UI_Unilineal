using UI_Unilineal.Domain.Connections;
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
        GroupSceneElement egress =
            Group(
                scene,
                "detail/B1/branch/C1/destination/egress");

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
            Anchor(egress, "N").Point,
            neutralOut.Points[^1]);
        Assert.Equal(
            Anchor(egress, "PE").Point,
            protectiveEarth.Points[^1]);
        Assert.DoesNotContain(
            protectiveEarth.Points,
            point =>
                point == Anchor(rcd, "N_IN").Point ||
                point == Anchor(rcd, "N_OUT").Point);
    }

    [Fact]
    public void CircuitEgress_KeepsTpLeftAndNeutralRightInsideBoard()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement frame =
            Group(
                scene,
                "detail/B1");
        GroupSceneElement egress =
            Group(
                scene,
                "detail/B1/branch/C1/destination/egress");

        SceneAnchor pe =
            Anchor(
                egress,
                "PE");
        SceneAnchor neutral =
            Anchor(
                egress,
                "N");

        Assert.True(pe.Point.X < neutral.Point.X);
        Assert.Equal(AnchorDirection.Left, pe.Direction);
        Assert.Equal(AnchorDirection.Right, neutral.Direction);
        Assert.True(Contains(frame.Bounds, pe.Point));
        Assert.True(Contains(frame.Bounds, neutral.Point));
    }

    [Fact]
    public void MinimalBoard_Ric18BusGrammar_SingleCircuitSharesIncomingCenterNode()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement frame =
            Group(scene, "detail/B1");
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

        double boardCenterX =
            frame.Bounds.X +
            (frame.Bounds.Width / 2.0);

        Assert.Equal(
            boardCenterX,
            incoming.Point.X);
        Assert.Equal(
            incoming.Point.X,
            branch.Point.X);
        Assert.True(rail.Start.X <= incoming.Point.X);
        Assert.True(rail.End.X >= incoming.Point.X);

        CircleSceneElement[] connectionNodes =
            scene.Elements
                .OfType<CircleSceneElement>()
                .Where(element =>
                    element.Id.Value.StartsWith(
                        "detail/B1/bus/BUS:B1:MAIN/node/",
                        StringComparison.Ordinal))
                .ToArray();

        CircleSceneElement node =
            Assert.Single(connectionNodes);
        Assert.Equal(
            incoming.Point,
            node.Center);
    }

    [Fact]
    public void MinimalBoard_MainBusRemainsAVisibleDistributionRail()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement bus =
            Group(
                scene,
                "detail/B1/bus/BUS:B1:MAIN");
        LineSceneElement rail =
            Assert.IsType<LineSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value ==
                    "detail/B1/bus/BUS:B1:MAIN/rail"));
        double incomingX =
            Anchor(
                bus,
                "IN").Point.X;

        Assert.True(rail.Start.X < incomingX);
        Assert.True(rail.End.X > incomingX);
        Assert.True(
            rail.End.X - rail.Start.X >=
            20);
    }

    [Fact]
    public void MinimalBoard_PrimaryPowerRoutesNeverBacktrackUpstream()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));

        PolylineSceneElement[] routes =
            scene.Elements
                .OfType<PolylineSceneElement>()
                .Where(route =>
                    route.LineStyleId == "POWER" &&
                    route.Metadata.ContainsKey(
                        "connectionId"))
                .ToArray();

        Assert.NotEmpty(routes);

        Assert.All(
            routes,
            route =>
            {
                Assert.True(
                    route.Points.Count <= 3,
                    $"Unexpected detour in {route.Id}: " +
                    string.Join(
                        " -> ",
                        route.Points));

                for (int index = 0;
                     index < route.Points.Count - 1;
                     index++)
                {
                    Assert.True(
                        route.Points[index + 1].Y >=
                        route.Points[index].Y,
                        $"Power route backtracks upstream: {route.Id}");
                }
            });
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

    [Theory]
    [InlineData("minimal")]
    [InlineData("nested")]
    public void BoardDetail_AuxiliaryRoutesStayInsideBoardAndEndAtCircuitEgress(
        string fixture)
    {
        SingleLineInput input =
            fixture == "minimal"
                ? SemanticFixtureFactory.Minimal()
                : SemanticFixtureFactory.NestedBoards();
        string circuitUid =
            fixture == "minimal"
                ? "C1"
                : "C4";
        DiagramScene scene =
            BuildScene(
                input,
                new EntityUid("B1"));
        GroupSceneElement frame =
            Group(
                scene,
                "detail/B1");
        GroupSceneElement egress =
            Group(
                scene,
                $"detail/B1/branch/{circuitUid}/destination/egress");

        PolylineSceneElement[] auxiliaryRoutes =
            scene.Elements
                .OfType<PolylineSceneElement>()
                .Where(route =>
                    route.Metadata.ContainsKey("connectionId") &&
                    route.LineStyleId is
                        "NEUTRAL_AUX" or
                        "GROUND_AUX")
                .ToArray();

        Assert.NotEmpty(auxiliaryRoutes);

        Assert.All(
            auxiliaryRoutes,
            route =>
            {
                Assert.All(
                    route.Points,
                    point =>
                        Assert.True(
                            Contains(
                                frame.Bounds,
                                point),
                            $"Auxiliary route escaped board frame: {route.Id} at {point}."));
            });

        Assert.Contains(
            auxiliaryRoutes,
            route =>
                route.Points[^1] ==
                Anchor(egress, "N").Point);
        Assert.Contains(
            auxiliaryRoutes,
            route =>
                route.Points[^1] ==
                Anchor(egress, "PE").Point);
    }

    [Fact]
    public void MinimalBoard_OnlyPowerContinuesFromBoardToExternalDestination()
    {
        DiagramScene scene =
            BuildScene(
                SemanticFixtureFactory.Minimal(),
                new EntityUid("B1"));
        GroupSceneElement frame =
            Group(
                scene,
                "detail/B1");
        GroupSceneElement destination =
            Group(
                scene,
                "detail/B1/branch/C1/destination");

        Assert.True(
            destination.Bounds.Y >=
            frame.Bounds.Bottom);

        PolylineSceneElement destinationPower =
            Route(
                scene,
                "detail/B1/connection/destination/C1");

        Assert.True(
            destinationPower.Points[^1].Y >=
            frame.Bounds.Bottom);

        Assert.DoesNotContain(
            scene.Connections,
            connection =>
                connection.Target.ElementId ==
                destination.Id &&
                connection.LineStyleId is
                    "NEUTRAL_AUX" or
                    "GROUND_AUX");
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

    private static bool Contains(
        MmRect bounds,
        MmPoint point) =>
        point.X >= bounds.X &&
        point.X <= bounds.Right &&
        point.Y >= bounds.Y &&
        point.Y <= bounds.Bottom;

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
