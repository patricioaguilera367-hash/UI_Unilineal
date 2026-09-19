using System.Collections.ObjectModel;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Scene;

public abstract class SceneElement
{
    protected SceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        IEnumerable<SceneAnchor>? anchors = null)
    {
        Id = id;
        Bounds = bounds;
        Layer = layer;
        ZIndex = zIndex;
        Visibility = visibility;
        SemanticReference = semanticReference;
        Metadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(
                metadata ?? new Dictionary<string, string>(),
                StringComparer.Ordinal));
        Anchors = Array.AsReadOnly((anchors ?? []).ToArray());
    }

    public SceneId Id { get; }

    public MmRect Bounds { get; }

    public SceneLayer Layer { get; }

    public int ZIndex { get; }

    public SceneVisibility Visibility { get; }

    public EntityReference? SemanticReference { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IReadOnlyList<SceneAnchor> Anchors { get; }
}

public sealed class LineSceneElement : SceneElement
{
    public LineSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        MmPoint start,
        MmPoint end,
        string lineStyleId)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        LineStyleId = Required(lineStyleId, nameof(lineStyleId));
        Start = start;
        End = end;
    }

    public MmPoint Start { get; }

    public MmPoint End { get; }

    public string LineStyleId { get; }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}

public sealed class PolylineSceneElement : SceneElement
{
    public PolylineSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        IEnumerable<MmPoint> points,
        string lineStyleId)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        ArgumentNullException.ThrowIfNull(points);
        MmPoint[] copied = points.ToArray();
        if (copied.Length < 2)
        {
            throw new ArgumentException(
                "Polyline requires at least two points.",
                nameof(points));
        }

        if (string.IsNullOrWhiteSpace(lineStyleId))
        {
            throw new ArgumentException("Line style ID is required.", nameof(lineStyleId));
        }

        Points = Array.AsReadOnly(copied);
        LineStyleId = lineStyleId;
    }

    public IReadOnlyList<MmPoint> Points { get; }

    public string LineStyleId { get; }
}

public sealed class RectangleSceneElement : SceneElement
{
    public RectangleSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        string lineStyleId)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        if (string.IsNullOrWhiteSpace(lineStyleId))
        {
            throw new ArgumentException("Line style ID is required.", nameof(lineStyleId));
        }

        LineStyleId = lineStyleId;
    }

    public string LineStyleId { get; }
}

public sealed class CircleSceneElement : SceneElement
{
    public CircleSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        MmPoint center,
        double radiusMm,
        string lineStyleId)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        if (!double.IsFinite(radiusMm) || radiusMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMm));
        }

        if (string.IsNullOrWhiteSpace(lineStyleId))
        {
            throw new ArgumentException("Line style ID is required.", nameof(lineStyleId));
        }

        Center = center;
        RadiusMm = radiusMm;
        LineStyleId = lineStyleId;
    }

    public MmPoint Center { get; }

    public double RadiusMm { get; }

    public string LineStyleId { get; }
}

public sealed class PathSceneElement : SceneElement
{
    public PathSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        string data,
        string lineStyleId)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            throw new ArgumentException("Path data is required.", nameof(data));
        }

        if (string.IsNullOrWhiteSpace(lineStyleId))
        {
            throw new ArgumentException("Line style ID is required.", nameof(lineStyleId));
        }

        Data = data;
        LineStyleId = lineStyleId;
    }

    public string Data { get; }

    public string LineStyleId { get; }
}

public enum SceneTextHorizontalAlignment
{
    Start,
    Center,
    End
}

public enum SceneTextVerticalAlignment
{
    Top,
    Center,
    Bottom
}

public sealed class TextSceneElement : SceneElement
{
    public TextSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        string text,
        string textStyleId,
        SceneTextHorizontalAlignment horizontalAlignment =
            SceneTextHorizontalAlignment.Start,
        SceneTextVerticalAlignment verticalAlignment =
            SceneTextVerticalAlignment.Top)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text is required.", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(textStyleId))
        {
            throw new ArgumentException("Text style ID is required.", nameof(textStyleId));
        }

        Text = text;
        TextStyleId = textStyleId;
        HorizontalAlignment = horizontalAlignment;
        VerticalAlignment = verticalAlignment;
    }

    public string Text { get; }

    public string TextStyleId { get; }

    public SceneTextHorizontalAlignment HorizontalAlignment { get; }

    public SceneTextVerticalAlignment VerticalAlignment { get; }
}

public sealed class SymbolSceneElement : SceneElement
{
    public SymbolSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        string symbolDefinitionId,
        double rotationDegrees = 0)
        : base(id, bounds, layer, zIndex, visibility, semanticReference, metadata)
    {
        if (string.IsNullOrWhiteSpace(symbolDefinitionId))
        {
            throw new ArgumentException(
                "Symbol definition ID is required.",
                nameof(symbolDefinitionId));
        }

        if (!double.IsFinite(rotationDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        }

        SymbolDefinitionId = symbolDefinitionId;
        RotationDegrees = rotationDegrees;
    }

    public string SymbolDefinitionId { get; }

    public double RotationDegrees { get; }
}

public sealed class GroupSceneElement : SceneElement
{
    public GroupSceneElement(
        SceneId id,
        MmRect bounds,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference,
        IReadOnlyDictionary<string, string>? metadata,
        IEnumerable<SceneId> childIds,
        IEnumerable<SceneAnchor>? anchors = null)
        : base(
            id,
            bounds,
            layer,
            zIndex,
            visibility,
            semanticReference,
            metadata,
            anchors)
    {
        ArgumentNullException.ThrowIfNull(childIds);
        ChildIds = Array.AsReadOnly(childIds.ToArray());
    }

    public IReadOnlyList<SceneId> ChildIds { get; }
}
