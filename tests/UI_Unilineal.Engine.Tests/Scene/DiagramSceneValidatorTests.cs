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
