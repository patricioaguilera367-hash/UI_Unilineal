using System.Globalization;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Layout;

public sealed class RoutedConnection
{
    public RoutedConnection(
        SceneId connectionId,
        IEnumerable<MmPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        MmPoint[] materialized = points.ToArray();

        if (materialized.Length < 2)
        {
            throw new ArgumentException(
                "A routed connection requires at least two points.",
                nameof(points));
        }

        ConnectionId = connectionId;
        Points = Array.AsReadOnly(materialized);
    }

    public SceneId ConnectionId { get; }

    public IReadOnlyList<MmPoint> Points { get; }
}

public sealed class OrthogonalConnectionRouter
{
    public RoutedConnection Route(
        SceneConnection connection,
        DiagramScene scene,
        LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(profile);

        Dictionary<SceneId, SceneElement> elements =
            scene.Elements.ToDictionary(element => element.Id);

        SceneElement sourceElement = ResolveElement(
            elements,
            connection.Source.ElementId,
            "source");
        SceneElement targetElement = ResolveElement(
            elements,
            connection.Target.ElementId,
            "target");

        SceneAnchor sourceAnchor = ResolveAnchor(
            sourceElement,
            connection.Source.AnchorId,
            "source");
        SceneAnchor targetAnchor = ResolveAnchor(
            targetElement,
            connection.Target.AnchorId,
            "target");

        if (!AnchorCompatibility.CanConnect(
                sourceAnchor.Role,
                targetAnchor.Role))
        {
            throw new InvalidOperationException(
                $"Connection '{connection.Id}' uses incompatible anchor roles " +
                $"'{sourceAnchor.Role}' -> '{targetAnchor.Role}'.");
        }

        if (sourceAnchor.Role == AnchorRole.BusTap &&
            targetAnchor.Role == AnchorRole.PowerIn)
        {
            return new RoutedConnection(
                connection.Id,
                CompactVerticalRoute(
                    sourceAnchor.Point,
                    targetAnchor.Point));
        }

        if (IsSameBranchPath(
                sourceElement,
                targetElement) &&
            sourceAnchor.Role == AnchorRole.PowerOut &&
            targetAnchor.Role == AnchorRole.PowerIn)
        {
            return new RoutedConnection(
                connection.Id,
                CompactVerticalRoute(
                    sourceAnchor.Point,
                    targetAnchor.Point));
        }

        if (IsAuxiliaryConductor(connection.LineStyleId))
        {
            return new RoutedConnection(
                connection.Id,
                RouteAuxiliaryConductor(
                    connection.LineStyleId,
                    scene,
                    sourceElement,
                    targetElement,
                    sourceAnchor,
                    targetAnchor,
                    profile));
        }

        MmRect[] obstacles =
            BuildObstacles(
                scene,
                sourceElement,
                targetElement,
                profile);

        IReadOnlyList<MmPoint> points = FindRoute(
            sourceAnchor.Point,
            targetAnchor.Point,
            obstacles,
            profile);

        return new RoutedConnection(
            connection.Id,
            points);
    }

    private static bool IsAuxiliaryConductor(
        string lineStyleId) =>
        lineStyleId is "NEUTRAL_AUX" or "GROUND_AUX";

    private static IReadOnlyList<MmPoint> CompactVerticalRoute(
        MmPoint start,
        MmPoint end) =>
        NormalizeRoute(
            start.X == end.X
                ? [start, end]
                : [
                    start,
                    new MmPoint(start.X, end.Y),
                    end
                ]);

    private static IReadOnlyList<MmPoint> RouteAuxiliaryConductor(
        string lineStyleId,
        DiagramScene scene,
        SceneElement sourceElement,
        SceneElement targetElement,
        SceneAnchor sourceAnchor,
        SceneAnchor targetAnchor,
        LayoutProfile profile)
    {
        MmPoint start =
            sourceAnchor.Point;
        MmPoint end =
            targetAnchor.Point;
        string? sourceRole =
            CompositionRole(sourceElement);
        bool leavesStructuralRail =
            sourceRole is "NeutralBus" or "ProtectiveEarthBus";
        Ric18BoardLayoutTokens tokens =
            Ric18BoardLayoutTokens.From(profile);

        MmRect[] obstacles =
            BuildObstacles(
                scene,
                sourceElement,
                targetElement,
                profile);

        IReadOnlyList<MmPoint>? semanticLane =
            TryRouteAuxiliaryLane(
                lineStyleId,
                scene,
                targetElement,
                sourceAnchor,
                targetAnchor,
                obstacles,
                tokens,
                leavesStructuralRail);

        if (semanticLane is not null)
        {
            return semanticLane;
        }

        MmPoint departure =
            OffsetFromAnchor(
                start,
                sourceAnchor.Direction,
                leavesStructuralRail
                    ? tokens.AuxiliaryRailDepartureMm
                    : tokens.AuxiliaryAnchorApproachMm);
        MmPoint approach =
            OffsetFromAnchor(
                end,
                targetAnchor.Direction,
                tokens.AuxiliaryAnchorApproachMm);

        IReadOnlyList<MmPoint> continuation =
            FindRoute(
                departure,
                approach,
                obstacles,
                profile);

        var points =
            new List<MmPoint>
            {
                start
            };
        points.AddRange(continuation);
        points.Add(end);

        return NormalizeRoute(points);
    }

    private static IReadOnlyList<MmPoint>? TryRouteAuxiliaryLane(
        string lineStyleId,
        DiagramScene scene,
        SceneElement targetElement,
        SceneAnchor sourceAnchor,
        SceneAnchor targetAnchor,
        IReadOnlyList<MmRect> obstacles,
        Ric18BoardLayoutTokens tokens,
        bool leavesStructuralRail)
    {
        MmPoint start =
            sourceAnchor.Point;
        MmPoint end =
            targetAnchor.Point;
        if (end.Y < start.Y)
        {
            return null;
        }

        bool neutral =
            lineStyleId == "NEUTRAL_AUX";
        MmRect routingEnvelope =
            AuxiliaryRoutingEnvelope(
                scene,
                targetElement);

        double laneX =
            neutral
                ? routingEnvelope.Right +
                  tokens.AuxiliaryLaneOffsetMm
                : routingEnvelope.X -
                  tokens.AuxiliaryLaneOffsetMm;

        MmPoint departure =
            OffsetFromAnchor(
                start,
                leavesStructuralRail
                    ? AnchorDirection.Down
                    : sourceAnchor.Direction,
                leavesStructuralRail
                    ? tokens.AuxiliaryRailDepartureMm
                    : tokens.AuxiliaryAnchorApproachMm);
        MmPoint approach =
            OffsetFromAnchor(
                end,
                targetAnchor.Direction,
                tokens.AuxiliaryAnchorApproachMm);

        var candidate =
            new List<MmPoint>
            {
                start,
                departure,
                new MmPoint(
                    laneX,
                    departure.Y),
                new MmPoint(
                    laneX,
                    approach.Y),
                approach,
                end
            };

        IReadOnlyList<MmPoint> normalized =
            Simplify(candidate);

        return
            normalized.Count >= 2 &&
            IsOrthogonal(normalized) &&
            IsClear(normalized, obstacles)
                ? normalized
                : null;
    }

    private static MmPoint OffsetFromAnchor(
        MmPoint point,
        AnchorDirection direction,
        double distance) =>
        direction switch
        {
            AnchorDirection.Left =>
                new MmPoint(
                    point.X - distance,
                    point.Y),
            AnchorDirection.Right =>
                new MmPoint(
                    point.X + distance,
                    point.Y),
            AnchorDirection.Up =>
                new MmPoint(
                    point.X,
                    point.Y - distance),
            AnchorDirection.Down =>
                new MmPoint(
                    point.X,
                    point.Y + distance),
            AnchorDirection.Any =>
                point,
            _ => throw new ArgumentOutOfRangeException(
                nameof(direction),
                direction,
                "Unsupported anchor direction.")
        };

    private static MmRect[] BuildObstacles(
        DiagramScene scene,
        SceneElement sourceElement,
        SceneElement targetElement,
        LayoutProfile profile)
    {
        IEnumerable<(string Id, MmRect Bounds)> structural =
            scene.Elements
                .OfType<GroupSceneElement>()
                .Where(group =>
                    group.Id != sourceElement.Id &&
                    group.Id != targetElement.Id &&
                    !IsStructuralRail(group))
                .Select(group =>
                    (
                        group.Id.Value,
                        group.Bounds
                    ));

        IEnumerable<(string Id, MmRect Bounds)> annotations =
            scene.Elements
                .OfType<TextSceneElement>()
                .Where(text =>
                    !IsOwnedByEndpoint(
                        text.Id,
                        sourceElement.Id) &&
                    !IsOwnedByEndpoint(
                        text.Id,
                        targetElement.Id))
                .Select(text =>
                    (
                        text.Id.Value,
                        text.Bounds
                    ));

        return structural
            .Concat(annotations)
            .OrderBy(
                item => item.Id,
                StringComparer.Ordinal)
            .Select(item =>
                Inflate(
                    item.Bounds,
                    profile.RouteClearanceMm))
            .ToArray();
    }

    private static MmRect AuxiliaryRoutingEnvelope(
        DiagramScene scene,
        SceneElement targetElement)
    {
        string? branchPrefix =
            BranchPrefix(
                targetElement.Id.Value);

        if (branchPrefix is null)
        {
            return targetElement.Bounds;
        }

        GroupSceneElement[] branchGroups =
            scene.Elements
                .OfType<GroupSceneElement>()
                .Where(group =>
                    string.Equals(
                        group.Id.Value,
                        branchPrefix,
                        StringComparison.Ordinal) ||
                    group.Id.Value.StartsWith(
                        branchPrefix + "/",
                        StringComparison.Ordinal))
                .ToArray();

        if (branchGroups.Length == 0)
        {
            return targetElement.Bounds;
        }

        double left =
            branchGroups.Min(group =>
                group.Bounds.X);
        double top =
            branchGroups.Min(group =>
                group.Bounds.Y);
        double right =
            branchGroups.Max(group =>
                group.Bounds.Right);
        double bottom =
            branchGroups.Max(group =>
                group.Bounds.Bottom);

        return new MmRect(
            left,
            top,
            right - left,
            bottom - top);
    }

    private static string? BranchPrefix(
        string elementId)
    {
        const string marker =
            "/branch/";

        int markerIndex =
            elementId.IndexOf(
                marker,
                StringComparison.Ordinal);

        if (markerIndex < 0)
        {
            return null;
        }

        int circuitStart =
            markerIndex +
            marker.Length;
        int nextSlash =
            elementId.IndexOf(
                '/',
                circuitStart);

        return nextSlash < 0
            ? elementId
            : elementId[..nextSlash];
    }

    private static bool IsOwnedByEndpoint(
        SceneId candidateId,
        SceneId endpointId)
    {
        string candidate =
            candidateId.Value;
        string endpoint =
            endpointId.Value;

        return
            string.Equals(
                candidate,
                endpoint,
                StringComparison.Ordinal) ||
            candidate.StartsWith(
                endpoint + "/",
                StringComparison.Ordinal);
    }

    private static bool IsSameBranchPath(
        SceneElement source,
        SceneElement target)
    {
        string? sourceParent = ParentId(source);
        string? targetParent = ParentId(target);

        return
            (!string.IsNullOrWhiteSpace(sourceParent) &&
             string.Equals(
                 sourceParent,
                 targetParent,
                 StringComparison.Ordinal)) ||
            string.Equals(
                source.Id.Value,
                targetParent,
                StringComparison.Ordinal) ||
            string.Equals(
                target.Id.Value,
                sourceParent,
                StringComparison.Ordinal);
    }

    private static string? ParentId(
        SceneElement element) =>
        element.Metadata.TryGetValue(
            "parentId",
            out string? value)
                ? value
                : null;

    private static string? CompositionRole(
        SceneElement element) =>
        element.Metadata.TryGetValue(
            "compositionRole",
            out string? value)
                ? value
                : null;

    private static IReadOnlyList<MmPoint> NormalizeRoute(
        IReadOnlyList<MmPoint> points)
    {
        var normalized =
            new List<MmPoint>();

        foreach (MmPoint point in points)
        {
            if (normalized.Count == 0 ||
                normalized[^1] != point)
            {
                normalized.Add(point);
            }
        }

        return normalized;
    }

    private static bool IsStructuralRail(
        GroupSceneElement group)
    {
        if (!group.Metadata.TryGetValue(
                "compositionRole",
                out string? role))
        {
            return false;
        }

        return role is
            "BoardFrame" or
            "MainBus" or
            "NeutralBus" or
            "ProtectiveEarthBus";
    }

    private static SceneElement ResolveElement(
        IReadOnlyDictionary<SceneId, SceneElement> elements,
        SceneId id,
        string endpoint)
    {
        if (!elements.TryGetValue(id, out SceneElement? element))
        {
            throw new InvalidOperationException(
                $"Connection {endpoint} element '{id}' does not exist.");
        }

        return element;
    }

    private static SceneAnchor ResolveAnchor(
        SceneElement element,
        string anchorId,
        string endpoint) =>
        element.Anchors.SingleOrDefault(anchor =>
            string.Equals(
                anchor.Id,
                anchorId,
                StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Connection {endpoint} anchor '{anchorId}' does not exist on '{element.Id}'.");

    private static IReadOnlyList<MmPoint> FindRoute(
        MmPoint start,
        MmPoint end,
        IReadOnlyList<MmRect> obstacles,
        LayoutProfile profile)
    {
        var candidates = new List<IReadOnlyList<MmPoint>>();

        AddCandidate(candidates, [start, end]);
        AddCandidate(
            candidates,
            [start, new MmPoint(end.X, start.Y), end]);
        AddCandidate(
            candidates,
            [start, new MmPoint(start.X, end.Y), end]);

        SortedSet<double> xChannels =
            new()
            {
                start.X,
                end.X
            };
        SortedSet<double> yChannels =
            new()
            {
                start.Y,
                end.Y
            };

        foreach (MmRect obstacle in obstacles)
        {
            xChannels.Add(obstacle.X);
            xChannels.Add(obstacle.Right);
            yChannels.Add(obstacle.Y);
            yChannels.Add(obstacle.Bottom);
        }

        foreach (double x in xChannels)
        {
            AddCandidate(
                candidates,
                [
                    start,
                    new MmPoint(x, start.Y),
                    new MmPoint(x, end.Y),
                    end
                ]);
        }

        foreach (double y in yChannels)
        {
            AddCandidate(
                candidates,
                [
                    start,
                    new MmPoint(start.X, y),
                    new MmPoint(end.X, y),
                    end
                ]);
        }

        if (obstacles.Count > 0)
        {
            double outerPadding =
                profile.RouteClearanceMm + profile.GridMm;
            double left = Math.Min(
                    Math.Min(start.X, end.X),
                    obstacles.Min(rect => rect.X)) -
                outerPadding;
            double right = Math.Max(
                    Math.Max(start.X, end.X),
                    obstacles.Max(rect => rect.Right)) +
                outerPadding;
            double top = Math.Min(
                    Math.Min(start.Y, end.Y),
                    obstacles.Min(rect => rect.Y)) -
                outerPadding;
            double bottom = Math.Max(
                    Math.Max(start.Y, end.Y),
                    obstacles.Max(rect => rect.Bottom)) +
                outerPadding;

            foreach (double outerX in new[] { left, right })
            {
                foreach (double outerY in new[] { top, bottom })
                {
                    AddCandidate(
                        candidates,
                        [
                            start,
                            new MmPoint(start.X, outerY),
                            new MmPoint(outerX, outerY),
                            new MmPoint(outerX, end.Y),
                            end
                        ]);

                    AddCandidate(
                        candidates,
                        [
                            start,
                            new MmPoint(outerX, start.Y),
                            new MmPoint(outerX, outerY),
                            new MmPoint(end.X, outerY),
                            end
                        ]);
                }
            }
        }

        IReadOnlyList<MmPoint>? best = candidates
            .Select(Simplify)
            .Where(candidate =>
                candidate.Count >= 2 &&
                IsOrthogonal(candidate) &&
                IsClear(candidate, obstacles))
            .OrderBy(RouteCost)
            .ThenBy(RouteSignature, StringComparer.Ordinal)
            .FirstOrDefault();

        return best ??
            throw new InvalidOperationException(
                "No orthogonal route could be found for the connection.");
    }

    private static void AddCandidate(
        ICollection<IReadOnlyList<MmPoint>> candidates,
        IReadOnlyList<MmPoint> points) =>
        candidates.Add(points);

    private static IReadOnlyList<MmPoint> Simplify(
        IReadOnlyList<MmPoint> points)
    {
        var deduplicated = new List<MmPoint>();

        foreach (MmPoint point in points)
        {
            if (deduplicated.Count == 0 ||
                deduplicated[^1] != point)
            {
                deduplicated.Add(point);
            }
        }

        bool changed;

        do
        {
            changed = false;

            for (int index = 1; index < deduplicated.Count - 1; index++)
            {
                MmPoint previous = deduplicated[index - 1];
                MmPoint current = deduplicated[index];
                MmPoint next = deduplicated[index + 1];

                bool collinear =
                    (previous.X == current.X &&
                     current.X == next.X) ||
                    (previous.Y == current.Y &&
                     current.Y == next.Y);

                if (!collinear)
                {
                    continue;
                }

                deduplicated.RemoveAt(index);
                changed = true;
                break;
            }
        }
        while (changed);

        return deduplicated;
    }

    private static bool IsOrthogonal(
        IReadOnlyList<MmPoint> points)
    {
        for (int index = 0; index < points.Count - 1; index++)
        {
            MmPoint first = points[index];
            MmPoint second = points[index + 1];

            if (first.X != second.X &&
                first.Y != second.Y)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsClear(
        IReadOnlyList<MmPoint> points,
        IReadOnlyList<MmRect> obstacles)
    {
        for (int index = 0; index < points.Count - 1; index++)
        {
            foreach (MmRect obstacle in obstacles)
            {
                if (CrossesInterior(
                        points[index],
                        points[index + 1],
                        obstacle))
                {
                    return false;
                }
            }
        }

        return true;
    }

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

    private static double RouteCost(
        IReadOnlyList<MmPoint> points)
    {
        double distance = 0;

        for (int index = 0; index < points.Count - 1; index++)
        {
            distance +=
                Math.Abs(points[index + 1].X - points[index].X) +
                Math.Abs(points[index + 1].Y - points[index].Y);
        }

        int bends = Math.Max(0, points.Count - 2);

        return distance + (bends * 0.001);
    }

    private static string RouteSignature(
        IReadOnlyList<MmPoint> points) =>
        string.Join(
            ";",
            points.Select(point =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{point.X:R},{point.Y:R}")));

    private static MmRect Inflate(
        MmRect rect,
        double clearance) =>
        new(
            rect.X - clearance,
            rect.Y - clearance,
            rect.Width + (2 * clearance),
            rect.Height + (2 * clearance));
}
