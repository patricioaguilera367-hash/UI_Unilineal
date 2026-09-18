using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed class SceneSpatialIndex
{
    private readonly double _cellSizeMm;
    private readonly Dictionary<CellKey, List<SceneElement>> _cells = [];

    public SceneSpatialIndex(
        DiagramScene scene,
        double cellSizeMm = 50.0)
    {
        Scene = scene ??
            throw new ArgumentNullException(nameof(scene));

        if (!double.IsFinite(cellSizeMm) || cellSizeMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSizeMm));
        }

        _cellSizeMm = cellSizeMm;

        foreach (SceneElement element in scene.Elements)
        {
            foreach (CellKey cell in CellsFor(element.Bounds))
            {
                if (!_cells.TryGetValue(
                        cell,
                        out List<SceneElement>? bucket))
                {
                    bucket = [];
                    _cells.Add(cell, bucket);
                }

                bucket.Add(element);
            }
        }
    }

    public DiagramScene Scene { get; }

    public IReadOnlyList<SceneElement> Query(
        MmRect viewportMm) =>
        QueryCells(viewportMm)
            .Where(element =>
                Intersects(
                    element.Bounds,
                    viewportMm))
            .OrderBy(element => element.Layer)
            .ThenBy(element => element.ZIndex)
            .ThenBy(
                element => element.Id.Value,
                StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<SceneElement> QueryPoint(
        MmPoint point,
        double toleranceMm)
    {
        if (!double.IsFinite(toleranceMm) || toleranceMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceMm));
        }

        if (toleranceMm == 0)
        {
            CellKey cell = CellFor(point.X, point.Y);

            if (!_cells.TryGetValue(
                    cell,
                    out List<SceneElement>? bucket))
            {
                return [];
            }

            return bucket
                .Where(element =>
                    element.Bounds.Contains(point))
                .DistinctBy(element => element.Id)
                .OrderBy(element => element.Layer)
                .ThenBy(element => element.ZIndex)
                .ThenBy(
                    element => element.Id.Value,
                    StringComparer.Ordinal)
                .ToArray();
        }

        var area = new MmRect(
            point.X - toleranceMm,
            point.Y - toleranceMm,
            toleranceMm * 2.0,
            toleranceMm * 2.0);

        return Query(area);
    }

    private IEnumerable<SceneElement> QueryCells(
        MmRect area)
    {
        var seen = new HashSet<SceneId>();

        foreach (CellKey cell in CellsFor(area))
        {
            if (!_cells.TryGetValue(
                    cell,
                    out List<SceneElement>? bucket))
            {
                continue;
            }

            foreach (SceneElement element in bucket)
            {
                if (seen.Add(element.Id))
                {
                    yield return element;
                }
            }
        }
    }

    private IEnumerable<CellKey> CellsFor(
        MmRect bounds)
    {
        CellKey first =
            CellFor(bounds.X, bounds.Y);
        CellKey last =
            CellFor(bounds.Right, bounds.Bottom);

        for (long y = first.Y; y <= last.Y; y++)
        {
            for (long x = first.X; x <= last.X; x++)
            {
                yield return new CellKey(x, y);
            }
        }
    }

    private CellKey CellFor(
        double x,
        double y) =>
        new(
            CellCoordinate(x),
            CellCoordinate(y));

    private long CellCoordinate(double value)
    {
        double coordinate =
            Math.Floor(value / _cellSizeMm);

        if (coordinate < long.MinValue ||
            coordinate > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Scene coordinate exceeds spatial-index range.");
        }

        return (long)coordinate;
    }

    private static bool Intersects(
        MmRect first,
        MmRect second) =>
        first.X <= second.Right &&
        first.Right >= second.X &&
        first.Y <= second.Bottom &&
        first.Bottom >= second.Y;

    private readonly record struct CellKey(
        long X,
        long Y);
}
