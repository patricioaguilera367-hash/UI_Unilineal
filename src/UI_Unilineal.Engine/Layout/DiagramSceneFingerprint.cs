using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Layout;

public static class DiagramSceneFingerprint
{
    public static string Compute(DiagramScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var builder = new StringBuilder();

        Value(builder, "SCENE");
        Value(builder, scene.Id.Value);
        Value(builder, scene.Kind.ToString());
        Rect(builder, scene.Bounds);
        AppendMetadata(builder, scene.Metadata);

        foreach (SceneIssue issue in scene.Issues
                     .OrderBy(item => item.Code, StringComparer.Ordinal)
                     .ThenBy(item => item.Entity?.Uid.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.Field, StringComparer.Ordinal)
                     .ThenBy(item => item.Message, StringComparer.Ordinal))
        {
            Value(builder, "ISSUE");
            Value(builder, issue.Code);
            Value(builder, issue.Severity.ToString());
            Value(builder, issue.Message);
            Value(builder, issue.Entity?.Kind.ToString());
            Value(builder, issue.Entity?.Uid.ToString());
            Value(builder, issue.Field);
        }

        foreach (SceneElement element in scene.Elements
                     .OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            AppendElement(builder, element);
        }

        foreach (SceneConnection connection in scene.Connections
                     .OrderBy(item => item.Id.Value, StringComparer.Ordinal))
        {
            AppendConnection(builder, connection);
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendMetadata(
        StringBuilder builder,
        DiagramSceneMetadata metadata)
    {
        Value(builder, "METADATA");
        Value(builder, metadata.DrawingProfileId);
        Value(builder, metadata.DrawingProfileVersion);
        Value(builder, metadata.DrawingProfileFingerprint);
        Value(builder, metadata.LayoutEngineVersion);
        Value(builder, metadata.InputFingerprint);
        Value(builder, metadata.ProjectionFingerprint);
    }

    private static void AppendElement(
        StringBuilder builder,
        SceneElement element)
    {
        Value(builder, "ELEMENT");
        Value(builder, element.Id.Value);
        Value(builder, element.GetType().Name);
        Rect(builder, element.Bounds);
        Value(builder, element.Layer.ToString());
        Value(builder, element.ZIndex.ToString(CultureInfo.InvariantCulture));
        Value(builder, element.Visibility.ToString());
        Value(builder, element.SemanticReference?.Kind.ToString());
        Value(builder, element.SemanticReference?.Uid.ToString());

        foreach (KeyValuePair<string, string> pair in element.Metadata
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Value(builder, "META");
            Value(builder, pair.Key);
            Value(builder, pair.Value);
        }

        foreach (SceneAnchor anchor in element.Anchors
                     .OrderBy(anchor => anchor.Id, StringComparer.Ordinal))
        {
            Value(builder, "ANCHOR");
            Value(builder, anchor.Id);
            Value(builder, anchor.Role.ToString());
            Point(builder, anchor.Point);
            Value(builder, anchor.Direction.ToString());
        }

        switch (element)
        {
            case LineSceneElement line:
                Value(builder, "LINE");
                Point(builder, line.Start);
                Point(builder, line.End);
                Value(builder, line.LineStyleId);
                break;

            case PolylineSceneElement polyline:
                Value(builder, "POLYLINE");
                foreach (MmPoint point in polyline.Points)
                {
                    Point(builder, point);
                }
                Value(builder, polyline.LineStyleId);
                break;

            case RectangleSceneElement rectangle:
                Value(builder, "RECTANGLE");
                Value(builder, rectangle.LineStyleId);
                break;

            case CircleSceneElement circle:
                Value(builder, "CIRCLE");
                Point(builder, circle.Center);
                Number(builder, circle.RadiusMm);
                Value(builder, circle.LineStyleId);
                break;

            case PathSceneElement path:
                Value(builder, "PATH");
                Value(builder, path.Data);
                Value(builder, path.LineStyleId);
                break;

            case TextSceneElement text:
                Value(builder, "TEXT");
                Value(builder, text.Text);
                Value(builder, text.TextStyleId);
                break;

            case SymbolSceneElement symbol:
                Value(builder, "SYMBOL");
                Value(builder, symbol.SymbolDefinitionId);
                Number(builder, symbol.RotationDegrees);
                break;

            case GroupSceneElement group:
                Value(builder, "GROUP");
                foreach (SceneId childId in group.ChildIds
                             .OrderBy(id => id.Value, StringComparer.Ordinal))
                {
                    Value(builder, childId.Value);
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported scene element '{element.GetType().FullName}'.");
        }
    }

    private static void AppendConnection(
        StringBuilder builder,
        SceneConnection connection)
    {
        Value(builder, "CONNECTION");
        Value(builder, connection.Id.Value);
        Value(builder, connection.Source.ElementId.Value);
        Value(builder, connection.Source.AnchorId);
        Value(builder, connection.Target.ElementId.Value);
        Value(builder, connection.Target.AnchorId);
        Value(builder, connection.LineStyleId);
        Value(builder, connection.Layer.ToString());
        Value(builder, connection.ZIndex.ToString(CultureInfo.InvariantCulture));
        Value(builder, connection.Visibility.ToString());
        Value(builder, connection.SemanticReference?.Kind.ToString());
        Value(builder, connection.SemanticReference?.Uid.ToString());
    }

    private static void Point(StringBuilder builder, MmPoint point)
    {
        Number(builder, point.X);
        Number(builder, point.Y);
    }

    private static void Rect(StringBuilder builder, MmRect rect)
    {
        Number(builder, rect.X);
        Number(builder, rect.Y);
        Number(builder, rect.Width);
        Number(builder, rect.Height);
    }

    private static void Number(StringBuilder builder, double value) =>
        Value(builder, value.ToString("R", CultureInfo.InvariantCulture));

    private static void Value(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
