using System.Globalization;
using System.Text;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Layout;

internal static class SceneGoldenFormatter
{
    public static string Format(DiagramScene scene)
    {
        var builder = new StringBuilder();

        Append(
            builder,
            "SCENE",
            scene.Id.Value,
            scene.Kind,
            Rect(scene.Bounds),
            scene.Metadata.DrawingProfileId,
            scene.Metadata.DrawingProfileVersion,
            scene.Metadata.DrawingProfileFingerprint,
            scene.Metadata.LayoutEngineVersion,
            scene.Metadata.InputFingerprint,
            scene.Metadata.ProjectionFingerprint,
            DiagramSceneFingerprint.Compute(scene));

        Append(
            builder,
            "COUNTS",
            scene.Elements.Count,
            scene.Elements.OfType<GroupSceneElement>().Count(),
            scene.Connections.Count,
            scene.Elements
                .OfType<PolylineSceneElement>()
                .Count(element =>
                    element.Metadata.ContainsKey("connectionId")),
            scene.Issues.Count);

        foreach (SceneIssue issue in scene.Issues
                     .OrderBy(
                         issue => issue.Code,
                         StringComparer.Ordinal)
                     .ThenBy(
                         issue => issue.Entity?.Uid.Value,
                         StringComparer.Ordinal)
                     .ThenBy(
                         issue => issue.Field,
                         StringComparer.Ordinal))
        {
            Append(
                builder,
                "ISSUE",
                issue.Code,
                issue.Severity,
                issue.Entity?.Kind.ToString() ?? "-",
                issue.Entity?.Uid.ToString() ?? "-",
                issue.Field ?? "-",
                issue.Message);
        }

        foreach (GroupSceneElement group in scene.Elements
                     .OfType<GroupSceneElement>()
                     .OrderBy(
                         group => group.Id.Value,
                         StringComparer.Ordinal))
        {
            string anchors = string.Join(
                ",",
                group.Anchors
                    .OrderBy(
                        anchor => anchor.Id,
                        StringComparer.Ordinal)
                    .Select(anchor =>
                        $"{anchor.Id}:{anchor.Role}@{Point(anchor.Point)}"));

            Append(
                builder,
                "GROUP",
                group.Id.Value,
                Rect(group.Bounds),
                group.SemanticReference?.Kind.ToString() ?? "-",
                group.SemanticReference?.Uid.ToString() ?? "-",
                anchors.Length == 0 ? "-" : anchors);
        }

        foreach (PolylineSceneElement route in scene.Elements
                     .OfType<PolylineSceneElement>()
                     .Where(element =>
                         element.Metadata.ContainsKey("connectionId"))
                     .OrderBy(
                         element =>
                             element.Metadata["connectionId"],
                         StringComparer.Ordinal))
        {
            Append(
                builder,
                "ROUTE",
                route.Metadata["connectionId"],
                route.LineStyleId,
                string.Join(
                    ">",
                    route.Points.Select(Point)));
        }

        foreach (SceneConnection connection in scene.Connections
                     .OrderBy(
                         connection => connection.Id.Value,
                         StringComparer.Ordinal))
        {
            Append(
                builder,
                "CONNECTION",
                connection.Id.Value,
                $"{connection.Source.ElementId.Value}@{connection.Source.AnchorId}",
                $"{connection.Target.ElementId.Value}@{connection.Target.AnchorId}",
                connection.LineStyleId);
        }

        return builder.ToString();
    }

    private static string Rect(MmRect value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:R},{value.Y:R},{value.Width:R},{value.Height:R}");

    private static string Point(MmPoint value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:R},{value.Y:R}");

    private static void Append(
        StringBuilder builder,
        params object[] values)
    {
        builder.AppendJoin('|', values);
        builder.Append('\n');
    }
}
