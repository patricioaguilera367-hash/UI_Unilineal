using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Scene;

public sealed class DiagramSceneFingerprintTests
{
    [Fact]
    public void Compute_ShuffledElementsAndConnections_IsStable()
    {
        DiagramScene source = DiagramSceneValidatorTests.ValidScene();
        var reversed = new DiagramScene(
            source.Bounds,
            source.Elements.Reverse(),
            source.Metadata,
            source.Connections.Reverse());

        Assert.Equal(
            DiagramSceneFingerprint.Compute(source),
            DiagramSceneFingerprint.Compute(reversed));
    }

    [Fact]
    public void Compute_ChangingAnchorGeometry_ChangesFingerprint()
    {
        DiagramScene source = DiagramSceneValidatorTests.ValidScene();
        GroupSceneElement original =
            Assert.IsType<GroupSceneElement>(source.Elements[0]);
        var changed = new GroupSceneElement(
            original.Id,
            original.Bounds,
            original.Layer,
            original.ZIndex,
            original.Visibility,
            original.SemanticReference,
            original.Metadata,
            original.ChildIds,
            [
                new SceneAnchor(
                    "OUT",
                    AnchorRole.PowerOut,
                    new MmPoint(19, 10),
                    AnchorDirection.Right)
            ]);
        var changedScene = new DiagramScene(
            source.Bounds,
            [changed, source.Elements[1]],
            source.Metadata,
            source.Connections);

        Assert.NotEqual(
            DiagramSceneFingerprint.Compute(source),
            DiagramSceneFingerprint.Compute(changedScene));
    }

    [Fact]
    public void Compute_ChangingMetadata_ChangesFingerprint()
    {
        DiagramScene source = DiagramSceneValidatorTests.ValidScene();
        var metadata = new DiagramSceneMetadata(
            source.Metadata.DrawingProfileId,
            source.Metadata.DrawingProfileVersion,
            "CHANGED-PROFILE",
            source.Metadata.LayoutEngineVersion,
            source.Metadata.InputFingerprint,
            source.Metadata.ProjectionFingerprint);
        var changed = new DiagramScene(
            source.Bounds,
            source.Elements,
            metadata,
            source.Connections);

        Assert.NotEqual(
            DiagramSceneFingerprint.Compute(source),
            DiagramSceneFingerprint.Compute(changed));
    }
}
