using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;

internal static class RenderingSceneFixtures
{
    public static RectangleSceneElement Rectangle(
        string id,
        MmRect bounds,
        SceneLayer layer = SceneLayer.Symbol,
        int zIndex = 20) =>
        new(
            new SceneId(id),
            bounds,
            layer,
            zIndex,
            SceneVisibility.Both,
            null,
            null,
            "POWER");

    public static DiagramScene Scene(
        IEnumerable<SceneElement> elements,
        MmRect? bounds = null)
    {
        SceneElement[] materialized = elements.ToArray();
        MmRect sceneBounds = bounds ??
            BoundsOf(materialized);

        return new DiagramScene(
            sceneBounds,
            materialized,
            new DiagramSceneMetadata(
                "RIC18-V1",
                "1.0.0",
                "PROFILE",
                "G6",
                "INPUT",
                "PROJECTION"));
    }

    private static MmRect BoundsOf(
        IReadOnlyList<SceneElement> elements)
    {
        if (elements.Count == 0)
        {
            return new MmRect(0, 0, 1, 1);
        }

        double minX = elements.Min(x => x.Bounds.X);
        double minY = elements.Min(x => x.Bounds.Y);
        double maxX = elements.Max(x => x.Bounds.Right);
        double maxY = elements.Max(x => x.Bounds.Bottom);

        return new MmRect(
            minX,
            minY,
            Math.Max(maxX - minX, 0.001),
            Math.Max(maxY - minY, 0.001));
    }
}
