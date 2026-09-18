using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Scene;

public enum SceneIssueSeverity
{
    Error,
    Warning
}

public sealed record SceneIssue(
    string Code,
    SceneIssueSeverity Severity,
    string Message,
    EntityReference? Entity = null,
    string? Field = null);

public sealed class DiagramSceneMetadata
{
    public DiagramSceneMetadata(
        string drawingProfileId,
        string drawingProfileVersion,
        string drawingProfileFingerprint,
        string layoutEngineVersion,
        string inputFingerprint,
        string projectionFingerprint)
    {
        DrawingProfileId = Require(drawingProfileId, nameof(drawingProfileId));
        DrawingProfileVersion = Require(
            drawingProfileVersion,
            nameof(drawingProfileVersion));
        DrawingProfileFingerprint = Require(
            drawingProfileFingerprint,
            nameof(drawingProfileFingerprint));
        LayoutEngineVersion = Require(
            layoutEngineVersion,
            nameof(layoutEngineVersion));
        InputFingerprint = Require(
            inputFingerprint,
            nameof(inputFingerprint));
        ProjectionFingerprint = Require(
            projectionFingerprint,
            nameof(projectionFingerprint));
    }

    public string DrawingProfileId { get; }

    public string DrawingProfileVersion { get; }

    public string DrawingProfileFingerprint { get; }

    public string LayoutEngineVersion { get; }

    public string InputFingerprint { get; }

    public string ProjectionFingerprint { get; }

    private static string Require(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}

public sealed class DiagramScene
{
    public DiagramScene(
        MmRect bounds,
        IEnumerable<SceneElement> elements,
        DiagramSceneMetadata metadata,
        IEnumerable<SceneConnection>? connections = null)
        : this(
            new SceneId("scene/anonymous"),
            DiagramSceneKind.Unknown,
            bounds,
            elements,
            metadata,
            connections,
            [])
    {
    }

    public DiagramScene(
        SceneId id,
        DiagramSceneKind kind,
        MmRect bounds,
        IEnumerable<SceneElement> elements,
        DiagramSceneMetadata metadata,
        IEnumerable<SceneConnection>? connections = null,
        IEnumerable<SceneIssue>? issues = null)
    {
        ArgumentNullException.ThrowIfNull(elements);
        ArgumentNullException.ThrowIfNull(issues);

        Id = id;
        Kind = kind;
        Bounds = bounds;
        Elements = Array.AsReadOnly(elements.ToArray());
        Connections = Array.AsReadOnly((connections ?? []).ToArray());
        Issues = Array.AsReadOnly(issues.ToArray());
        Metadata = metadata ??
            throw new ArgumentNullException(nameof(metadata));
    }

    public SceneId Id { get; }

    public DiagramSceneKind Kind { get; }

    public MmRect Bounds { get; }

    public IReadOnlyList<SceneElement> Elements { get; }

    public IReadOnlyList<SceneConnection> Connections { get; }

    public IReadOnlyList<SceneIssue> Issues { get; }

    public DiagramSceneMetadata Metadata { get; }
}
