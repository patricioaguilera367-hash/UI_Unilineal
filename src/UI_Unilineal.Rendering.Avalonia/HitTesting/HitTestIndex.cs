using Avalonia;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.HitTesting;

public sealed class HitTestIndex
{
    private readonly DiagramScene _scene;
    private readonly SceneSpatialIndex _spatialIndex;

    public HitTestIndex(
        DiagramScene scene,
        SceneSpatialIndex spatialIndex)
    {
        _scene = scene ??
            throw new ArgumentNullException(nameof(scene));
        _spatialIndex = spatialIndex ??
            throw new ArgumentNullException(nameof(spatialIndex));

        if (!ReferenceEquals(
                _scene,
                _spatialIndex.Scene))
        {
            throw new ArgumentException(
                "Spatial index must belong to the supplied scene.",
                nameof(spatialIndex));
        }
    }

    public IReadOnlyList<HitTestResult> HitTest(
        Point pointerDip,
        ViewportState viewport,
        HitTestPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(policy);

        MmPoint pointer =
            ViewportTransform.DipToSceneMm(
                pointerDip,
                viewport);
        double scale =
            ViewportTransform.DipsPerMillimetre *
            viewport.Zoom;
        double toleranceMm =
            policy.ToleranceDip / scale;
        double anchorRadiusMm =
            policy.AnchorRadiusDip / scale;
        double searchRadiusMm =
            Math.Max(
                toleranceMm,
                anchorRadiusMm);

        IReadOnlyList<SceneElement> candidates =
            _spatialIndex.QueryPoint(
                pointer,
                searchRadiusMm);
        var hits =
            new List<HitTestResult>();

        foreach (SceneElement element in candidates)
        {
            AddAnchorHits(
                element,
                pointer,
                anchorRadiusMm,
                hits);

            double distance =
                DistanceToElement(
                    element,
                    pointer);

            if (distance <= toleranceMm)
            {
                HitKind kind =
                    KindFor(element);
                hits.Add(
                    new HitTestResult(
                        element.Id,
                        element.SemanticReference,
                        kind,
                        null,
                        distance,
                        Priority(kind)));
            }
        }

        return hits
            .OrderByDescending(hit => hit.Priority)
            .ThenBy(hit => hit.DistanceMm)
            .ThenBy(
                hit => hit.SceneElementId.Value,
                StringComparer.Ordinal)
            .ThenBy(
                hit => hit.AnchorId,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddAnchorHits(
        SceneElement element,
        MmPoint pointer,
        double radiusMm,
        ICollection<HitTestResult> hits)
    {
        foreach (SceneAnchor anchor in element.Anchors)
        {
            double distance =
                Distance(
                    pointer,
                    anchor.Point);

            if (distance > radiusMm)
            {
                continue;
            }

            hits.Add(
                new HitTestResult(
                    element.Id,
                    element.SemanticReference,
                    HitKind.Anchor,
                    anchor.Id,
                    distance,
                    Priority(HitKind.Anchor)));
        }
    }

    private static HitKind KindFor(
        SceneElement element)
    {
        if (element.Layer == SceneLayer.Background)
        {
            return HitKind.Background;
        }

        return element switch
        {
            SymbolSceneElement =>
                HitKind.Symbol,
            TextSceneElement =>
                HitKind.SemanticText,
            GroupSceneElement group
                when group.SemanticReference?.Kind ==
                     EntityKind.Circuit =>
                HitKind.Branch,
            GroupSceneElement =>
                HitKind.Group,
            LineSceneElement =>
                HitKind.Connection,
            PolylineSceneElement =>
                HitKind.Connection,
            RectangleSceneElement =>
                HitKind.Symbol,
            CircleSceneElement =>
                HitKind.Symbol,
            PathSceneElement =>
                HitKind.Symbol,
            _ => HitKind.Group
        };
    }

    private static int Priority(
        HitKind kind) =>
        kind switch
        {
            HitKind.Anchor => 700,
            HitKind.Symbol => 600,
            HitKind.SemanticText => 500,
            HitKind.Branch => 400,
            HitKind.Connection => 300,
            HitKind.Group => 200,
            HitKind.Background => 100,
            _ => 0
        };

    private static double DistanceToElement(
        SceneElement element,
        MmPoint point) =>
        element switch
        {
            LineSceneElement line =>
                DistanceToSegment(
                    point,
                    line.Start,
                    line.End),
            PolylineSceneElement polyline =>
                DistanceToPolyline(
                    point,
                    polyline.Points),
            _ =>
                DistanceToRect(
                    point,
                    element.Bounds)
        };

    private static double DistanceToPolyline(
        MmPoint point,
        IReadOnlyList<MmPoint> points)
    {
        double best =
            double.PositiveInfinity;

        for (int index = 0;
             index < points.Count - 1;
             index++)
        {
            best = Math.Min(
                best,
                DistanceToSegment(
                    point,
                    points[index],
                    points[index + 1]));
        }

        return best;
    }

    private static double DistanceToSegment(
        MmPoint point,
        MmPoint first,
        MmPoint second)
    {
        double dx =
            second.X - first.X;
        double dy =
            second.Y - first.Y;

        if (dx == 0 && dy == 0)
        {
            return Distance(
                point,
                first);
        }

        double lengthSquared =
            (dx * dx) + (dy * dy);
        double t =
            (((point.X - first.X) * dx) +
             ((point.Y - first.Y) * dy)) /
            lengthSquared;
        t = Math.Clamp(t, 0, 1);

        var projected = new MmPoint(
            first.X + (t * dx),
            first.Y + (t * dy));

        return Distance(
            point,
            projected);
    }

    private static double DistanceToRect(
        MmPoint point,
        MmRect bounds)
    {
        double dx =
            Math.Max(
                Math.Max(
                    bounds.X - point.X,
                    0),
                point.X - bounds.Right);
        double dy =
            Math.Max(
                Math.Max(
                    bounds.Y - point.Y,
                    0),
                point.Y - bounds.Bottom);

        return Math.Sqrt(
            (dx * dx) +
            (dy * dy));
    }

    private static double Distance(
        MmPoint first,
        MmPoint second)
    {
        double dx =
            first.X - second.X;
        double dy =
            first.Y - second.Y;

        return Math.Sqrt(
            (dx * dx) +
            (dy * dy));
    }
}
