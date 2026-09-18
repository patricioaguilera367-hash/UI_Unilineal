using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Domain.Symbols;

public abstract record SymbolPrimitive(string LineStyleId);

public sealed record LineSymbolPrimitive(
    MmPoint Start,
    MmPoint End,
    string StyleId)
    : SymbolPrimitive(StyleId);

public sealed record RectangleSymbolPrimitive(
    MmRect Rectangle,
    string StyleId)
    : SymbolPrimitive(StyleId);

public sealed record CircleSymbolPrimitive(
    MmPoint Center,
    double Radius,
    string StyleId)
    : SymbolPrimitive(StyleId);

public sealed record ArcSymbolPrimitive(
    MmPoint Center,
    double Radius,
    double StartDegrees,
    double SweepDegrees,
    string StyleId)
    : SymbolPrimitive(StyleId);

public sealed record PathSymbolPrimitive(
    string Data,
    string StyleId)
    : SymbolPrimitive(StyleId);

public sealed record PolylineSymbolPrimitive : SymbolPrimitive
{
    public PolylineSymbolPrimitive(
        IEnumerable<MmPoint> points,
        string styleId)
        : base(styleId)
    {
        ArgumentNullException.ThrowIfNull(points);
        Points = Array.AsReadOnly(points.ToArray());
    }

    public IReadOnlyList<MmPoint> Points { get; }
}

public sealed record AnchorDefinition
{
    public AnchorDefinition(
        string id,
        AnchorRole role,
        MmPoint point,
        AnchorDirection direction)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Anchor ID is required.", nameof(id));
        }

        Id = id;
        Role = role;
        Point = point;
        Direction = direction;
    }

    public string Id { get; }

    public AnchorRole Role { get; }

    public MmPoint Point { get; }

    public AnchorDirection Direction { get; }
}

public sealed record LabelSlot
{
    public LabelSlot(
        string id,
        MmRect bounds,
        int priority,
        bool required,
        string textStyleId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Label slot ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(textStyleId))
        {
            throw new ArgumentException("Text style ID is required.", nameof(textStyleId));
        }

        Id = id;
        Bounds = bounds;
        Priority = priority;
        Required = required;
        TextStyleId = textStyleId;
    }

    public string Id { get; }

    public MmRect Bounds { get; }

    public int Priority { get; }

    public bool Required { get; }

    public string TextStyleId { get; }
}

public sealed class SymbolDefinition
{
    public SymbolDefinition(
        string id,
        string semanticRole,
        MmRect nominalBounds,
        IEnumerable<AnchorDefinition> anchors,
        IEnumerable<SymbolPrimitive> primitives,
        IEnumerable<LabelSlot> labelSlots,
        IEnumerable<string> provenanceIds)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Symbol ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(semanticRole))
        {
            throw new ArgumentException("Semantic role is required.", nameof(semanticRole));
        }

        Id = id;
        SemanticRole = semanticRole;
        NominalBounds = nominalBounds;
        Anchors = CopyUniqueById(anchors, nameof(anchors), x => x.Id);
        Primitives = Copy(primitives, nameof(primitives));
        LabelSlots = CopyUniqueById(labelSlots, nameof(labelSlots), x => x.Id);
        ProvenanceIds = CopyUniqueStrings(provenanceIds, nameof(provenanceIds));
    }

    public string Id { get; }

    public string SemanticRole { get; }

    public MmRect NominalBounds { get; }

    public IReadOnlyList<AnchorDefinition> Anchors { get; }

    public IReadOnlyList<SymbolPrimitive> Primitives { get; }

    public IReadOnlyList<LabelSlot> LabelSlots { get; }

    public IReadOnlyList<string> ProvenanceIds { get; }

    private static IReadOnlyList<T> Copy<T>(
        IEnumerable<T> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }

    private static IReadOnlyList<T> CopyUniqueById<T>(
        IEnumerable<T> values,
        string parameterName,
        Func<T, string> idSelector)
    {
        T[] materialized = Copy(values, parameterName).ToArray();
        string? duplicate = materialized
            .GroupBy(idSelector, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate ID '{duplicate}'.",
                parameterName);
        }

        return Array.AsReadOnly(materialized);
    }

    private static IReadOnlyList<string> CopyUniqueStrings(
        IEnumerable<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] materialized = values.ToArray();

        if (materialized.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Values cannot contain blanks.",
                parameterName);
        }

        string? duplicate = materialized
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Duplicate ID '{duplicate}'.",
                parameterName);
        }

        return Array.AsReadOnly(materialized);
    }
}
