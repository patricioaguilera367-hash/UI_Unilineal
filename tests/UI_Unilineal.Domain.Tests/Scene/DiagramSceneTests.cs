using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Scene;

public sealed class DiagramSceneTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("5d2f7a9a-4099-4dd8-a6d4-97c1b0e93d14")]
    [InlineData("scene id")]
    public void SceneId_RejectsBlankRandomOrWhitespaceIdentifiers(string value)
    {
        Assert.Throws<ArgumentException>(() => new SceneId(value));
    }

    [Fact]
    public void SceneElement_CarriesNeutralSceneMetadata()
    {
        var entity = new EntityReference(new EntityUid("B1"), EntityKind.Board);
        var element = new RectangleSceneElement(
            new SceneId("detail/B1/block/frame"),
            new MmRect(10, 20, 30, 40),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            entity,
            new Dictionary<string, string>
            {
                ["role"] = "frame"
            },
            "POWER");

        Assert.Equal(SceneLayer.Symbol, element.Layer);
        Assert.Equal(20, element.ZIndex);
        Assert.Equal(SceneVisibility.Both, element.Visibility);
        Assert.Equal(entity, element.SemanticReference);
        Assert.Equal(new MmRect(10, 20, 30, 40), element.Bounds);
        Assert.Equal("frame", element.Metadata["role"]);
    }

    [Fact]
    public void DiagramScene_DefensivelyCopiesElementsAndMetadata()
    {
        var element = new TextSceneElement(
            new SceneId("summary/B1/text/code"),
            new MmRect(0, 0, 20, 5),
            SceneLayer.Text,
            30,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>(),
            "TGBT",
            "TECH");
        var elements = new List<SceneElement> { element };
        var metadata = new DiagramSceneMetadata(
            "RIC18-V1",
            "1.0.0",
            "PROFILE-FP",
            "G4",
            "INPUT-FP",
            "PROJECTION-FP");

        var scene = new DiagramScene(
            new MmRect(0, 0, 100, 100),
            elements,
            metadata);

        elements.Clear();

        Assert.Single(scene.Elements);
        Assert.Equal("PROFILE-FP", scene.Metadata.DrawingProfileFingerprint);
        Assert.Equal(new MmRect(0, 0, 100, 100), scene.Bounds);
    }

    [Fact]
    public void NeutralScene_ExposesAllRequiredPrimitiveElementTypes()
    {
        Type[] required =
        [
            typeof(LineSceneElement),
            typeof(PolylineSceneElement),
            typeof(RectangleSceneElement),
            typeof(CircleSceneElement),
            typeof(PathSceneElement),
            typeof(TextSceneElement),
            typeof(SymbolSceneElement),
            typeof(GroupSceneElement)
        ];

        Assert.Equal(8, required.Distinct().Count());
        Assert.All(required, type => Assert.True(typeof(SceneElement).IsAssignableFrom(type)));
    }

    [Fact]
    public void SceneLayersAndVisibility_AreRendererNeutral()
    {
        SceneLayer[] expectedLayers =
        [
            SceneLayer.Background,
            SceneLayer.Power,
            SceneLayer.Protection,
            SceneLayer.Grounding,
            SceneLayer.Symbol,
            SceneLayer.Text,
            SceneLayer.Annotation,
            SceneLayer.StatusOverlay,
            SceneLayer.Interaction
        ];

        Assert.Equal(expectedLayers, Enum.GetValues<SceneLayer>());
        Assert.Equal(
            [SceneVisibility.Print, SceneVisibility.Interactive, SceneVisibility.Both],
            Enum.GetValues<SceneVisibility>());
    }
}
