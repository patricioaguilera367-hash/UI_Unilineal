using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Scene;

public sealed class DiagramSceneValidatorTests
{
    [Fact]
    public void Validate_ValidScene_HasNoErrors()
    {
        DiagramSceneValidationResult result =
            new DiagramSceneValidator().Validate(ValidScene());

        Assert.False(result.HasErrors);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_DuplicateElementIds_ReturnsError()
    {
        DiagramScene source = ValidScene();
        SceneElement duplicate = source.Elements[0];

        var scene = new DiagramScene(
            source.Bounds,
            [.. source.Elements, duplicate],
            source.Metadata,
            source.Connections);

        AssertCode(scene, SceneValidationCodes.DuplicateSceneId);
    }

    [Fact]
    public void Validate_DefaultNonPositiveBounds_ReturnsError()
    {
        var element = new RectangleSceneElement(
            new SceneId("scene/invalid/bounds"),
            default,
            SceneLayer.Symbol,
            1,
            SceneVisibility.Both,
            null,
            null,
            "POWER");
        var scene = new DiagramScene(
            new MmRect(0, 0, 100, 100),
            [element],
            Metadata());

        AssertCode(scene, SceneValidationCodes.InvalidBounds);
    }

    [Fact]
    public void Validate_MissingAnchor_ReturnsError()
    {
        DiagramScene source = ValidScene();
        SceneConnection invalid = source.Connections[0] with
        {
            Target = new SceneAnchorRef(
                source.Connections[0].Target.ElementId,
                "MISSING")
        };
        var scene = new DiagramScene(
            source.Bounds,
            source.Elements,
            source.Metadata,
            [invalid]);

        AssertCode(scene, SceneValidationCodes.MissingAnchor);
    }

    [Fact]
    public void Validate_OrphanConnectionElement_ReturnsError()
    {
        DiagramScene source = ValidScene();
        SceneConnection invalid = source.Connections[0] with
        {
            Source = new SceneAnchorRef(
                new SceneId("scene/missing/source"),
                "OUT")
        };
        var scene = new DiagramScene(
            source.Bounds,
            source.Elements,
            source.Metadata,
            [invalid]);

        AssertCode(scene, SceneValidationCodes.OrphanConnection);
    }

    [Fact]
    public void Validate_ElementOutsideSceneBounds_ReturnsError()
    {
        DiagramScene source = ValidScene();
        var outside = new GroupSceneElement(
            new SceneId("scene/outside/group"),
            new MmRect(95, 95, 20, 20),
            SceneLayer.Symbol,
            1,
            SceneVisibility.Both,
            null,
            null,
            []);

        var scene = new DiagramScene(
            source.Bounds,
            [.. source.Elements, outside],
            source.Metadata,
            source.Connections);

        AssertCode(scene, SceneValidationCodes.ElementOutsideSceneBounds);
    }

    [Fact]
    public void StrictValidation_RoutedPolylineCrossingUnrelatedGroup_ReturnsError()
    {
        GroupSceneElement source = Group(
            "scene/source/group",
            new MmRect(0, 0, 20, 20),
            new SceneAnchor(
                "OUT",
                AnchorRole.PowerOut,
                new MmPoint(20, 10),
                AnchorDirection.Right));
        GroupSceneElement obstacle = Group(
            "scene/obstacle/group",
            new MmRect(30, 0, 20, 20));
        GroupSceneElement target = Group(
            "scene/target/group",
            new MmRect(60, 0, 20, 20),
            new SceneAnchor(
                "IN",
                AnchorRole.PowerIn,
                new MmPoint(60, 10),
                AnchorDirection.Left));
        var connection = new SceneConnection(
            new SceneId("scene/connection/source-target"),
            new SceneAnchorRef(source.Id, "OUT"),
            new SceneAnchorRef(target.Id, "IN"),
            "POWER",
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null);
        var route = new PolylineSceneElement(
            new SceneId("scene/connection/source-target/route"),
            new MmRect(20, 9.9995, 40, 0.001),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>
            {
                ["connectionId"] = connection.Id.Value
            },
            [new MmPoint(20, 10), new MmPoint(60, 10)],
            "POWER");
        var scene = new DiagramScene(
            new MmRect(0, 0, 100, 100),
            [source, obstacle, target, route],
            Metadata(),
            [connection]);

        DiagramSceneValidationResult result =
            new DiagramSceneValidator().Validate(
                scene,
                SceneValidationMode.Strict);

        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                SceneValidationCodes.RouteIntersectsStructuralBlock);
    }

    [Fact]
    public void StrictValidation_BoardFrameAndStructuralRails_AreNotObstacles()
    {
        var frame = new GroupSceneElement(
            new SceneId("detail/B1"),
            new MmRect(0, 0, 100, 100),
            SceneLayer.Symbol,
            0,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>
            {
                ["compositionRole"] = "BoardFrame"
            },
            []);
        GroupSceneElement source = Group(
            "detail/B1/source",
            new MmRect(10, 10, 10, 10),
            new SceneAnchor(
                "OUT",
                AnchorRole.PowerOut,
                new MmPoint(20, 15),
                AnchorDirection.Right));
        var mainBus = new GroupSceneElement(
            new SceneId("detail/B1/bus/main"),
            new MmRect(25, 40, 50, 10),
            SceneLayer.Symbol,
            5,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>
            {
                ["compositionRole"] = "MainBus"
            },
            []);
        GroupSceneElement target = Group(
            "detail/B1/target",
            new MmRect(80, 75, 10, 10),
            new SceneAnchor(
                "IN",
                AnchorRole.PowerIn,
                new MmPoint(80, 80),
                AnchorDirection.Left));
        var connection = new SceneConnection(
            new SceneId("detail/B1/connection"),
            new SceneAnchorRef(source.Id, "OUT"),
            new SceneAnchorRef(target.Id, "IN"),
            "POWER",
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null);
        var route = new PolylineSceneElement(
            new SceneId("detail/B1/connection/route"),
            new MmRect(20, 15, 60, 65),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>
            {
                ["connectionId"] = connection.Id.Value
            },
            [
                new MmPoint(20, 15),
                new MmPoint(50, 15),
                new MmPoint(50, 80),
                new MmPoint(80, 80)
            ],
            "POWER");
        var scene = new DiagramScene(
            new MmRect(0, 0, 100, 100),
            [frame, source, mainBus, target, route],
            Metadata(),
            [connection]);

        DiagramSceneValidationResult result =
            new DiagramSceneValidator().Validate(
                scene,
                SceneValidationMode.Strict);

        Assert.DoesNotContain(
            result.Issues,
            issue =>
                issue.Code == SceneValidationCodes.StructuralBlockOverlap ||
                issue.Code == SceneValidationCodes.RouteIntersectsStructuralBlock);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void GeometryContracts_RejectNonFinitePointsBeforeSceneValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MmPoint(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MmPoint(0, double.PositiveInfinity));
    }

    [Fact]
    public void Validate_IssuesAreDeterministicallySorted()
    {
        DiagramScene source = ValidScene();
        var orphan = source.Connections[0] with
        {
            Source = new SceneAnchorRef(
                new SceneId("scene/missing/source"),
                "OUT")
        };
        var duplicate = source.Elements[0];

        var scene = new DiagramScene(
            source.Bounds,
            [.. source.Elements, duplicate],
            source.Metadata,
            [orphan]);

        DiagramSceneValidationResult result =
            new DiagramSceneValidator().Validate(scene);

        string[] signatures = result.Issues
            .Select(x => $"{x.Code}|{x.SceneId}|{x.Field}|{x.Message}")
            .ToArray();
        Assert.Equal(
            signatures.OrderBy(x => x, StringComparer.Ordinal),
            signatures);
    }

    private static void AssertCode(DiagramScene scene, string code)
    {
        DiagramSceneValidationResult result =
            new DiagramSceneValidator().Validate(scene);

        Assert.Contains(result.Issues, x => x.Code == code);
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

    internal static DiagramScene ValidScene()
    {
        var source = new GroupSceneElement(
            new SceneId("scene/source/group"),
            new MmRect(0, 0, 20, 20),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            [],
            [
                new SceneAnchor(
                    "OUT",
                    AnchorRole.PowerOut,
                    new MmPoint(20, 10),
                    AnchorDirection.Right)
            ]);
        var target = new GroupSceneElement(
            new SceneId("scene/target/group"),
            new MmRect(40, 0, 20, 20),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            [],
            [
                new SceneAnchor(
                    "IN",
                    AnchorRole.PowerIn,
                    new MmPoint(40, 10),
                    AnchorDirection.Left)
            ]);
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
            new MmRect(0, 0, 100, 100),
            [source, target],
            Metadata(),
            [connection]);
    }

    internal static DiagramSceneMetadata Metadata() =>
        new(
            "RIC18-V1",
            "1.0.0",
            "PROFILE",
            "G4",
            "INPUT",
            "PROJECTION");
}
