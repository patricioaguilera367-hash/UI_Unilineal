using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Rendering;

public sealed class SceneSpatialIndexTests
{
    [Fact]
    public void Query_CrossingCells_ReturnsOnlyIntersectingElements()
    {
        SceneElement first = RenderingSceneFixtures.Rectangle(
            "scene/item/A",
            new MmRect(5, 5, 10, 10));
        SceneElement second = RenderingSceneFixtures.Rectangle(
            "scene/item/B",
            new MmRect(55, 5, 10, 10));
        SceneElement third = RenderingSceneFixtures.Rectangle(
            "scene/item/C",
            new MmRect(105, 5, 10, 10));
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [first, second, third],
            new MmRect(0, 0, 150, 50));

        var index = new SceneSpatialIndex(
            scene,
            cellSizeMm: 50);

        IReadOnlyList<SceneElement> result =
            index.Query(
                new MmRect(40, 0, 40, 30));

        Assert.Equal(
            [second.Id],
            result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void Query_ElementSpanningMultipleCells_IsReturnedOnce()
    {
        SceneElement spanning = RenderingSceneFixtures.Rectangle(
            "scene/item/spanning",
            new MmRect(25, 25, 100, 100));
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [spanning],
            new MmRect(0, 0, 150, 150));

        var index = new SceneSpatialIndex(
            scene,
            cellSizeMm: 50);

        IReadOnlyList<SceneElement> result =
            index.Query(
                new MmRect(0, 0, 150, 150));

        Assert.Single(result);
        Assert.Equal(spanning.Id, result[0].Id);
    }

    [Fact]
    public void Query_ShuffledEquivalentScene_UsesStableRenderOrdering()
    {
        SceneElement high = RenderingSceneFixtures.Rectangle(
            "scene/item/Z",
            new MmRect(0, 0, 10, 10),
            SceneLayer.Text,
            30);
        SceneElement laterId = RenderingSceneFixtures.Rectangle(
            "scene/item/B",
            new MmRect(20, 0, 10, 10),
            SceneLayer.Symbol,
            20);
        SceneElement earlierId = RenderingSceneFixtures.Rectangle(
            "scene/item/A",
            new MmRect(40, 0, 10, 10),
            SceneLayer.Symbol,
            20);

        DiagramScene normal = RenderingSceneFixtures.Scene(
            [high, laterId, earlierId],
            new MmRect(0, 0, 60, 20));
        DiagramScene reversed = RenderingSceneFixtures.Scene(
            [earlierId, laterId, high],
            new MmRect(0, 0, 60, 20));
        var query = new MmRect(0, 0, 60, 20);

        SceneId[] first =
            new SceneSpatialIndex(normal, 25)
                .Query(query)
                .Select(x => x.Id)
                .ToArray();
        SceneId[] second =
            new SceneSpatialIndex(reversed, 25)
                .Query(query)
                .Select(x => x.Id)
                .ToArray();

        Assert.Equal(first, second);
        Assert.Equal(
            [
                new SceneId("scene/item/A"),
                new SceneId("scene/item/B"),
                new SceneId("scene/item/Z")
            ],
            first);
    }

    [Fact]
    public void QueryPoint_UsesToleranceAndStableOrdering()
    {
        SceneElement first = RenderingSceneFixtures.Rectangle(
            "scene/item/A",
            new MmRect(10, 10, 10, 10));
        SceneElement second = RenderingSceneFixtures.Rectangle(
            "scene/item/B",
            new MmRect(18, 10, 10, 10));
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [second, first],
            new MmRect(0, 0, 40, 40));
        var index = new SceneSpatialIndex(scene, 20);

        IReadOnlyList<SceneElement> result =
            index.QueryPoint(
                new MmPoint(20, 15),
                toleranceMm: 1);

        Assert.Equal(
            [
                new SceneId("scene/item/A"),
                new SceneId("scene/item/B")
            ],
            result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void LargeRegularScene_QueryReturnsSmallIntersectingSubset()
    {
        var elements = new List<SceneElement>(5000);

        for (int row = 0; row < 50; row++)
        {
            for (int column = 0; column < 100; column++)
            {
                int index = (row * 100) + column;
                elements.Add(
                    RenderingSceneFixtures.Rectangle(
                        $"scene/grid/item-{index:D4}",
                        new MmRect(
                            column * 10.0,
                            row * 10.0,
                            8,
                            8)));
            }
        }

        DiagramScene scene = RenderingSceneFixtures.Scene(
            elements,
            new MmRect(0, 0, 1000, 500));
        var spatial = new SceneSpatialIndex(
            scene,
            cellSizeMm: 40);
        var viewport = new MmRect(
            200,
            120,
            25,
            25);

        IReadOnlyList<SceneElement> result =
            spatial.Query(viewport);

        Assert.NotEmpty(result);
        Assert.True(result.Count < scene.Elements.Count / 20);
        Assert.All(
            result,
            element => Assert.True(
                Intersects(
                    element.Bounds,
                    viewport),
                element.Id.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_InvalidCellSize_Throws(
        double cellSizeMm)
    {
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [
                RenderingSceneFixtures.Rectangle(
                    "scene/item/A",
                    new MmRect(0, 0, 10, 10))
            ]);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SceneSpatialIndex(
                scene,
                cellSizeMm));
    }

    private static bool Intersects(
        MmRect first,
        MmRect second) =>
        first.X <= second.Right &&
        first.Right >= second.X &&
        first.Y <= second.Bottom &&
        first.Bottom >= second.Y;
}
