namespace UI_Unilineal.Domain.Scene;

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
        DiagramSceneMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(elements);
        Bounds = bounds;
        Elements = Array.AsReadOnly(elements.ToArray());
        Metadata = metadata ??
            throw new ArgumentNullException(nameof(metadata));
    }

    public MmRect Bounds { get; }

    public IReadOnlyList<SceneElement> Elements { get; }

    public DiagramSceneMetadata Metadata { get; }
}
