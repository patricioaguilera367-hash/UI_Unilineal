using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Layout;

public sealed class DiagramSceneValidator
{
    public DiagramSceneValidationResult Validate(
        DiagramScene scene,
        SceneValidationMode mode = SceneValidationMode.Basic)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var issues = new List<SceneValidationIssue>();

        ValidateUniqueIds(scene, issues);
        bool sceneBoundsValid = BoundsAreValid(scene.Bounds);
        if (!sceneBoundsValid)
        {
            issues.Add(Error(
                SceneValidationCodes.InvalidBounds,
                "Scene bounds must be finite and have positive size.",
                "scene",
                nameof(DiagramScene.Bounds)));
        }

        Dictionary<SceneId, SceneElement> elements = scene.Elements
            .GroupBy(element => element.Id)
            .ToDictionary(
                group => group.Key,
                group => group.First());

        foreach (SceneElement element in scene.Elements)
        {
            ValidateElement(
                scene,
                element,
                elements,
                sceneBoundsValid,
                issues);
        }

        foreach (SceneConnection connection in scene.Connections)
        {
            ValidateConnection(connection, elements, issues);
        }

        if (mode == SceneValidationMode.Strict)
        {
            ValidateStructuralOverlaps(scene, issues);
            ValidateRoutedConnections(scene, issues);
        }

        SceneValidationIssue[] sorted = issues
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.SceneId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Field, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray();

        return new DiagramSceneValidationResult(sorted);
    }

    private static void ValidateUniqueIds(
        DiagramScene scene,
        ICollection<SceneValidationIssue> issues)
    {
        IEnumerable<(SceneId Id, string Kind)> ids =
            scene.Elements.Select(element => (element.Id, "element"))
                .Concat(scene.Connections.Select(connection => (connection.Id, "connection")));

        foreach (IGrouping<SceneId, (SceneId Id, string Kind)> group in ids
                     .GroupBy(item => item.Id)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                SceneValidationCodes.DuplicateSceneId,
                $"Scene ID '{group.Key}' is used more than once.",
                group.Key.ToString(),
                "Id"));
        }
    }

    private static void ValidateElement(
        DiagramScene scene,
        SceneElement element,
        IReadOnlyDictionary<SceneId, SceneElement> elements,
        bool sceneBoundsValid,
        ICollection<SceneValidationIssue> issues)
    {
        bool boundsValid = BoundsAreValid(element.Bounds);

        if (!boundsValid)
        {
            issues.Add(Error(
                SceneValidationCodes.InvalidBounds,
                $"Element '{element.Id}' has invalid bounds.",
                element.Id.ToString(),
                nameof(SceneElement.Bounds)));
        }
        else if (sceneBoundsValid && !Contains(scene.Bounds, element.Bounds))
        {
            issues.Add(Error(
                SceneValidationCodes.ElementOutsideSceneBounds,
                $"Element '{element.Id}' lies outside scene bounds.",
                element.Id.ToString(),
                nameof(SceneElement.Bounds)));
        }

        foreach (IGrouping<string, SceneAnchor> duplicate in element.Anchors
                     .GroupBy(anchor => anchor.Id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                SceneValidationCodes.DuplicateAnchorId,
                $"Anchor ID '{duplicate.Key}' is duplicated on element '{element.Id}'.",
                element.Id.ToString(),
                duplicate.Key));
        }

        if (boundsValid)
        {
            foreach (SceneAnchor anchor in element.Anchors)
            {
                if (!element.Bounds.Contains(anchor.Point))
                {
                    issues.Add(Error(
                        SceneValidationCodes.AnchorOutsideElementBounds,
                        $"Anchor '{anchor.Id}' lies outside element '{element.Id}'.",
                        element.Id.ToString(),
                        anchor.Id));
                }
            }
        }

        if (element is GroupSceneElement group)
        {
            foreach (SceneId childId in group.ChildIds)
            {
                if (!elements.ContainsKey(childId))
                {
                    issues.Add(Error(
                        SceneValidationCodes.OrphanGroupChild,
                        $"Group child '{childId}' does not exist.",
                        group.Id.ToString(),
                        nameof(GroupSceneElement.ChildIds)));
                }
            }
        }
    }

    private static void ValidateConnection(
        SceneConnection connection,
        IReadOnlyDictionary<SceneId, SceneElement> elements,
        ICollection<SceneValidationIssue> issues)
    {
        ValidateEndpoint(
            connection,
            connection.Source,
            "Source",
            elements,
            issues);
        ValidateEndpoint(
            connection,
            connection.Target,
            "Target",
            elements,
            issues);
    }

    private static void ValidateEndpoint(
        SceneConnection connection,
        SceneAnchorRef endpoint,
        string field,
        IReadOnlyDictionary<SceneId, SceneElement> elements,
        ICollection<SceneValidationIssue> issues)
    {
        if (!elements.TryGetValue(endpoint.ElementId, out SceneElement? element))
        {
            issues.Add(Error(
                SceneValidationCodes.OrphanConnection,
                $"Connection endpoint element '{endpoint.ElementId}' does not exist.",
                connection.Id.ToString(),
                field));
            return;
        }

        if (!element.Anchors.Any(anchor =>
                string.Equals(
                    anchor.Id,
                    endpoint.AnchorId,
                    StringComparison.Ordinal)))
        {
            issues.Add(Error(
                SceneValidationCodes.MissingAnchor,
                $"Anchor '{endpoint.AnchorId}' does not exist on element '{endpoint.ElementId}'.",
                connection.Id.ToString(),
                field));
        }
    }

    private static void ValidateStructuralOverlaps(
        DiagramScene scene,
        ICollection<SceneValidationIssue> issues)
    {
        GroupSceneElement[] groups = scene.Elements
            .OfType<GroupSceneElement>()
            .Where(group => BoundsAreValid(group.Bounds))
            .OrderBy(group => group.Id.Value, StringComparer.Ordinal)
            .ToArray();

        for (int left = 0; left < groups.Length; left++)
        {
            for (int right = left + 1; right < groups.Length; right++)
            {
                if (!Overlaps(
                        groups[left].Bounds,
                        groups[right].Bounds))
                {
                    continue;
                }

                issues.Add(Error(
                    SceneValidationCodes.StructuralBlockOverlap,
                    $"Structural blocks '{groups[left].Id}' and '{groups[right].Id}' overlap.",
                    groups[left].Id.ToString(),
                    nameof(SceneElement.Bounds)));
            }
        }
    }

    private static void ValidateRoutedConnections(
        DiagramScene scene,
        ICollection<SceneValidationIssue> issues)
    {
        IReadOnlyDictionary<string, SceneConnection> connections =
            scene.Connections
                .GroupBy(
                    connection => connection.Id.Value,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);

        GroupSceneElement[] groups = scene.Elements
            .OfType<GroupSceneElement>()
            .OrderBy(
                group => group.Id.Value,
                StringComparer.Ordinal)
            .ToArray();

        foreach (PolylineSceneElement route in scene.Elements
                     .OfType<PolylineSceneElement>()
                     .Where(element =>
                         element.Metadata.TryGetValue(
                             "connectionId",
                             out string? value) &&
                         !string.IsNullOrWhiteSpace(value))
                     .OrderBy(
                         element => element.Id.Value,
                         StringComparer.Ordinal))
        {
            string connectionId =
                route.Metadata["connectionId"];

            if (!connections.TryGetValue(
                    connectionId,
                    out SceneConnection? connection))
            {
                continue;
            }

            foreach (GroupSceneElement group in groups)
            {
                if (group.Id == connection.Source.ElementId ||
                    group.Id == connection.Target.ElementId)
                {
                    continue;
                }

                if (!RouteCrossesInterior(
                        route.Points,
                        group.Bounds))
                {
                    continue;
                }

                issues.Add(Error(
                    SceneValidationCodes.RouteIntersectsStructuralBlock,
                    $"Route '{route.Id}' intersects structural block '{group.Id}'.",
                    route.Id.ToString(),
                    nameof(PolylineSceneElement.Points)));
            }
        }
    }

    private static bool RouteCrossesInterior(
        IReadOnlyList<MmPoint> points,
        MmRect obstacle)
    {
        for (int index = 0; index < points.Count - 1; index++)
        {
            if (SegmentCrossesInterior(
                    points[index],
                    points[index + 1],
                    obstacle))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentCrossesInterior(
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

    private static bool Overlaps(MmRect first, MmRect second) =>
        first.X < second.Right &&
        first.Right > second.X &&
        first.Y < second.Bottom &&
        first.Bottom > second.Y;

    private static bool BoundsAreValid(MmRect bounds) =>
        double.IsFinite(bounds.X) &&
        double.IsFinite(bounds.Y) &&
        double.IsFinite(bounds.Width) &&
        double.IsFinite(bounds.Height) &&
        bounds.Width > 0 &&
        bounds.Height > 0;

    private static bool Contains(MmRect outer, MmRect inner) =>
        inner.X >= outer.X &&
        inner.Y >= outer.Y &&
        inner.Right <= outer.Right &&
        inner.Bottom <= outer.Bottom;

    private static SceneValidationIssue Error(
        string code,
        string message,
        string? sceneId = null,
        string? field = null) =>
        new(
            code,
            SceneValidationSeverity.Error,
            message,
            sceneId,
            field);
}
