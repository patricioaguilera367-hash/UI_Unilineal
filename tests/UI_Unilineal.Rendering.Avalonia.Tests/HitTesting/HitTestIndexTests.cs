using Avalonia;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Rendering.Avalonia.HitTesting;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Tests.HitTesting;

public sealed class HitTestIndexTests
{
    [Fact]
    public void HitTolerance_InDipConvertsThroughCurrentZoom()
    {
        var line = new LineSceneElement(
            new SceneId("scene/line/main"),
            new MmRect(0, 9.9995, 100, 0.001),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            null,
            new MmPoint(0, 10),
            new MmPoint(100, 10),
            "POWER");
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [line],
            new MmRect(0, 0, 120, 40));
        var hitIndex = new HitTestIndex(
            scene,
            new SceneSpatialIndex(scene, 25));
        var policy = new HitTestPolicy(
            toleranceDip: 6,
            anchorRadiusDip: 8);
        var lowZoom = new ViewportState(
            1,
            default,
            new Size(1000, 600));
        var highZoom = new ViewportState(
            4,
            default,
            new Size(1000, 600));

        Point oneMillimetreBelowAtLowZoom =
            ViewportTransform.SceneMmToDip(
                new MmPoint(50, 11),
                lowZoom);
        Point oneMillimetreBelowAtHighZoom =
            ViewportTransform.SceneMmToDip(
                new MmPoint(50, 11),
                highZoom);

        Assert.Single(
            hitIndex.HitTest(
                oneMillimetreBelowAtLowZoom,
                lowZoom,
                policy));
        Assert.Empty(
            hitIndex.HitTest(
                oneMillimetreBelowAtHighZoom,
                highZoom,
                policy));
    }

    [Fact]
    public void Anchor_OutranksOwningGroup()
    {
        var board = new EntityReference(
            new EntityUid("B1"),
            EntityKind.Board);
        var group = new GroupSceneElement(
            new SceneId("scene/board/B1"),
            new MmRect(10, 10, 30, 20),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            board,
            null,
            [],
            [
                new SceneAnchor(
                    "IN",
                    AnchorRole.PowerIn,
                    new MmPoint(10, 20),
                    AnchorDirection.Left)
            ]);
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [group],
            new MmRect(0, 0, 60, 50));
        var viewport = new ViewportState(
            1,
            default,
            new Size(800, 600));
        Point pointer =
            ViewportTransform.SceneMmToDip(
                new MmPoint(10, 20),
                viewport);

        IReadOnlyList<HitTestResult> hits =
            new HitTestIndex(
                scene,
                new SceneSpatialIndex(scene))
                .HitTest(
                    pointer,
                    viewport,
                    new HitTestPolicy());

        Assert.True(hits.Count >= 2);
        Assert.Equal(HitKind.Anchor, hits[0].Kind);
        Assert.Equal("IN", hits[0].AnchorId);
        Assert.Equal(group.Id, hits[0].SceneElementId);
        Assert.Contains(
            hits,
            hit => hit.Kind == HitKind.Group);
    }

    [Fact]
    public void PriorityMatrix_IsDeterministicAcrossOverlappingKinds()
    {
        var circuit = new EntityReference(
            new EntityUid("C1"),
            EntityKind.Circuit);
        var group = new GroupSceneElement(
            new SceneId("scene/branch/C1"),
            new MmRect(0, 0, 30, 30),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            circuit,
            null,
            []);
        var symbol = new SymbolSceneElement(
            new SceneId("scene/symbol/C1"),
            new MmRect(0, 0, 30, 30),
            SceneLayer.Symbol,
            25,
            SceneVisibility.Both,
            circuit,
            null,
            "BREAKER");
        var text = new TextSceneElement(
            new SceneId("scene/text/C1"),
            new MmRect(0, 0, 30, 30),
            SceneLayer.Text,
            30,
            SceneVisibility.Both,
            circuit,
            null,
            "C1",
            "TECH");
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [group, text, symbol],
            new MmRect(0, 0, 40, 40));
        var viewport = new ViewportState(
            1,
            default,
            new Size(400, 400));
        Point pointer =
            ViewportTransform.SceneMmToDip(
                new MmPoint(15, 15),
                viewport);

        HitKind[] kinds =
            new HitTestIndex(
                scene,
                new SceneSpatialIndex(scene))
                .HitTest(
                    pointer,
                    viewport,
                    new HitTestPolicy())
                .Select(hit => hit.Kind)
                .ToArray();

        Assert.Equal(
            [
                HitKind.Symbol,
                HitKind.SemanticText,
                HitKind.Branch
            ],
            kinds);
    }

    [Fact]
    public void ThinPolyline_IsSelectableByPointToSegmentDistance()
    {
        var route = new PolylineSceneElement(
            new SceneId("scene/route/C1"),
            new MmRect(10, 9.9995, 80, 20.001),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>
            {
                ["connectionId"] = "scene/connection/C1"
            },
            [
                new MmPoint(10, 10),
                new MmPoint(50, 10),
                new MmPoint(50, 30),
                new MmPoint(90, 30)
            ],
            "POWER");
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [route],
            new MmRect(0, 0, 100, 50));
        var viewport = new ViewportState(
            1,
            default,
            new Size(600, 400));
        Point pointer =
            ViewportTransform.SceneMmToDip(
                new MmPoint(52, 20),
                viewport);

        HitTestResult hit = Assert.Single(
            new HitTestIndex(
                scene,
                new SceneSpatialIndex(scene))
                .HitTest(
                    pointer,
                    viewport,
                    new HitTestPolicy(
                        toleranceDip: 8,
                        anchorRadiusDip: 8)));

        Assert.Equal(HitKind.Connection, hit.Kind);
        Assert.Equal(route.Id, hit.SceneElementId);
        Assert.Equal(2.0, hit.DistanceMm, 9);
    }

    [Fact]
    public void AmbiguousEqualPriorityHits_AreSortedByDistanceThenId()
    {
        var first = new SymbolSceneElement(
            new SceneId("scene/symbol/A"),
            new MmRect(10, 10, 20, 20),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            "BREAKER");
        var second = new SymbolSceneElement(
            new SceneId("scene/symbol/B"),
            new MmRect(10, 10, 20, 20),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            "BREAKER");
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [second, first],
            new MmRect(0, 0, 50, 50));
        var viewport = new ViewportState(
            1,
            default,
            new Size(400, 400));
        Point pointer =
            ViewportTransform.SceneMmToDip(
                new MmPoint(20, 20),
                viewport);

        SceneId[] ids =
            new HitTestIndex(
                scene,
                new SceneSpatialIndex(scene))
                .HitTest(
                    pointer,
                    viewport,
                    new HitTestPolicy())
                .Select(hit => hit.SceneElementId)
                .ToArray();

        Assert.Equal(
            [
                new SceneId("scene/symbol/A"),
                new SceneId("scene/symbol/B")
            ],
            ids);
    }
}
