using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class OrthogonalConnectionRouterTests
{
    [Fact]
    public void Route_ProducesOnlyHorizontalOrVerticalSegments()
    {
        DiagramScene scene = Scene(includeObstacle: false);
        SceneConnection connection = Assert.Single(scene.Connections);

        RoutedConnection routed =
            new OrthogonalConnectionRouter().Route(
                connection,
                scene,
                Profile().Layout);

        Assert.Equal(new MmPoint(20, 10), routed.Points[0]);
        Assert.Equal(new MmPoint(80, 10), routed.Points[^1]);
        AssertOrthogonal(routed.Points);
    }

    [Fact]
    public void Route_AvoidsInflatedStructuralObstacle()
    {
        RIC18DrawingProfile profile = Profile();
        DiagramScene scene = Scene(includeObstacle: true);
        SceneConnection connection = Assert.Single(scene.Connections);

        RoutedConnection routed =
            new OrthogonalConnectionRouter().Route(
                connection,
                scene,
                profile.Layout);

        Assert.True(routed.Points.Count >= 4);
        AssertOrthogonal(routed.Points);

        MmRect obstacle = Inflate(
            new MmRect(40, 0, 20, 20),
            profile.Layout.RouteClearanceMm);

        for (int index = 0; index < routed.Points.Count - 1; index++)
        {
            Assert.False(
                CrossesInterior(
                    routed.Points[index],
                    routed.Points[index + 1],
                    obstacle),
                $"Segment crosses clearance obstacle: {routed.Points[index]} -> {routed.Points[index + 1]}");
        }
    }

    [Fact]
    public void Route_IsDeterministicAcrossElementOrder()
    {
        RIC18DrawingProfile profile = Profile();
        DiagramScene source = Scene(includeObstacle: true);
        var shuffled = new DiagramScene(
            source.Bounds,
            source.Elements.Reverse(),
            source.Metadata,
            source.Connections.Reverse());

        RoutedConnection first =
            new OrthogonalConnectionRouter().Route(
                source.Connections[0],
                source,
                profile.Layout);
        RoutedConnection second =
            new OrthogonalConnectionRouter().Route(
                shuffled.Connections[0],
                shuffled,
                profile.Layout);

        Assert.Equal(first.Points, second.Points);
    }

    [Theory]
    [InlineData("NEUTRAL_AUX", "NeutralBus", 100, 55, true)]
    [InlineData("GROUND_AUX", "ProtectiveEarthBus", 20, 65, false)]
    public void Route_AuxiliaryRailUsesSemanticSideLaneBeforeGenericFallback(
        string lineStyleId,
        string sourceRole,
        double sourceX,
        double targetX,
        bool approachesFromRight)
    {
        RIC18DrawingProfile profile = Profile();
        AnchorRole role = lineStyleId == "NEUTRAL_AUX"
            ? AnchorRole.Neutral
            : AnchorRole.Ground;

        GroupSceneElement source = GroupWithRole(
            "scene/aux-source",
            new MmRect(sourceX - 10, 0, 20, 20),
            sourceRole,
            new SceneAnchor(
                "TAP:C1",
                role,
                new MmPoint(sourceX, 10),
                AnchorDirection.Down));
        GroupSceneElement target = Group(
            "scene/aux-target",
            new MmRect(40, 60, 40, 20),
            new SceneAnchor(
                lineStyleId == "NEUTRAL_AUX" ? "N" : "PE",
                role,
                new MmPoint(targetX, 70),
                AnchorDirection.Up));
        var connection = new SceneConnection(
            new SceneId("scene/connection/aux"),
            new SceneAnchorRef(source.Id, "TAP:C1"),
            new SceneAnchorRef(
                target.Id,
                lineStyleId == "NEUTRAL_AUX" ? "N" : "PE"),
            lineStyleId,
            lineStyleId == "GROUND_AUX"
                ? SceneLayer.Grounding
                : SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null);
        var scene = new DiagramScene(
            new MmRect(0, 0, 140, 100),
            [source, target],
            Metadata(),
            [connection]);

        RoutedConnection routed =
            new OrthogonalConnectionRouter().Route(
                connection,
                scene,
                profile.Layout);

        AssertOrthogonal(routed.Points);
        Assert.True(routed.Points[1].Y > routed.Points[0].Y);

        MmPoint approach = routed.Points[^2];

        if (approachesFromRight)
        {
            Assert.True(approach.X > target.Bounds.Right);
            Assert.True(approach.X > routed.Points[^1].X);
        }
        else
        {
            Assert.True(approach.X < target.Bounds.X);
            Assert.True(approach.X < routed.Points[^1].X);
        }
    }

    [Fact]
    public void Route_IncompatibleAnchorRoles_Throws()
    {
        RIC18DrawingProfile profile = Profile();
        GroupSceneElement source = Group(
            "scene/source",
            new MmRect(0, 0, 20, 20),
            new SceneAnchor(
                "IN",
                AnchorRole.PowerIn,
                new MmPoint(20, 10),
                AnchorDirection.Right));
        GroupSceneElement target = Group(
            "scene/target",
            new MmRect(80, 0, 20, 20),
            new SceneAnchor(
                "IN",
                AnchorRole.PowerIn,
                new MmPoint(80, 10),
                AnchorDirection.Left));
        var connection = new SceneConnection(
            new SceneId("scene/connection/test"),
            new SceneAnchorRef(source.Id, "IN"),
            new SceneAnchorRef(target.Id, "IN"),
            "POWER",
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null);
        var scene = new DiagramScene(
            new MmRect(0, 0, 120, 60),
            [source, target],
            Metadata(),
            [connection]);

        Assert.Throws<InvalidOperationException>(
            () => new OrthogonalConnectionRouter().Route(
                connection,
                scene,
                profile.Layout));
    }

    private static DiagramScene Scene(bool includeObstacle)
    {
        GroupSceneElement source = Group(
            "scene/source",
            new MmRect(0, 0, 20, 20),
            new SceneAnchor(
                "OUT",
                AnchorRole.PowerOut,
                new MmPoint(20, 10),
                AnchorDirection.Right));
        GroupSceneElement target = Group(
            "scene/target",
            new MmRect(80, 0, 20, 20),
            new SceneAnchor(
                "IN",
                AnchorRole.PowerIn,
                new MmPoint(80, 10),
                AnchorDirection.Left));

        var elements = new List<SceneElement>
        {
            source,
            target
        };

        if (includeObstacle)
        {
            elements.Add(Group(
                "scene/obstacle",
                new MmRect(40, 0, 20, 20)));
        }

        var connection = new SceneConnection(
            new SceneId("scene/connection/source-target"),
            new SceneAnchorRef(source.Id, "OUT"),
            new SceneAnchorRef(target.Id, "IN"),
            "POWER",
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null);

        return new DiagramScene(
            new MmRect(0, -30, 120, 80),
            elements,
            Metadata(),
            [connection]);
    }

    private static GroupSceneElement Group(
        string id,
        MmRect bounds,
        params SceneAnchor[] anchors) =>
        new(
            new SceneId(id),
            bounds,
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            [],
            anchors);

    private static GroupSceneElement GroupWithRole(
        string id,
        MmRect bounds,
        string compositionRole,
        params SceneAnchor[] anchors) =>
        new(
            new SceneId(id),
            bounds,
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["compositionRole"] = compositionRole
            },
            [],
            anchors);

    private static void AssertOrthogonal(
        IReadOnlyList<MmPoint> points)
    {
        Assert.True(points.Count >= 2);

        for (int index = 0; index < points.Count - 1; index++)
        {
            MmPoint first = points[index];
            MmPoint second = points[index + 1];

            Assert.True(
                first.X == second.X || first.Y == second.Y,
                $"Non-orthogonal segment: {first} -> {second}");
        }
    }

    private static MmRect Inflate(MmRect rect, double clearance) =>
        new(
            rect.X - clearance,
            rect.Y - clearance,
            rect.Width + (2 * clearance),
            rect.Height + (2 * clearance));

    private static bool CrossesInterior(
        MmPoint first,
        MmPoint second,
        MmRect obstacle)
    {
        if (first.Y == second.Y)
        {
            double min = Math.Min(first.X, second.X);
            double max = Math.Max(first.X, second.X);

            return first.Y > obstacle.Y &&
                   first.Y < obstacle.Bottom &&
                   Math.Max(min, obstacle.X) <
                   Math.Min(max, obstacle.Right);
        }

        if (first.X == second.X)
        {
            double min = Math.Min(first.Y, second.Y);
            double max = Math.Max(first.Y, second.Y);

            return first.X > obstacle.X &&
                   first.X < obstacle.Right &&
                   Math.Max(min, obstacle.Y) <
                   Math.Min(max, obstacle.Bottom);
        }

        return true;
    }

    private static DiagramSceneMetadata Metadata() =>
        new(
            "RIC18-V1",
            "1.0.0",
            "PROFILE",
            "G5-ROUTER",
            "INPUT",
            "PROJECTION");

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
