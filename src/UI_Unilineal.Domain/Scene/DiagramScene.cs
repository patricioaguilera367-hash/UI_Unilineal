namespace UI_Unilineal.Domain.Scene;

public sealed record DiagramSceneMetadata(
    string DrawingProfileId,
    string DrawingProfileVersion,
    string DrawingProfileFingerprint,
    string LayoutEngineVersion,
    string InputFingerprint,
    string ProjectionFingerprint)
{
    public DiagramSceneMetadata
    {
        Require(DrawingProfileId, nameof(DrawingProfileId));
        Require(DrawingProfileVersion, nameof(DrawingProfileVersion));
        Require(DrawingProfileFingerprint, nameof(DrawingProfileFingerprint));
        Require(LayoutEngineVersion, nameof(LayoutEngineVersion));
        Require(InputFingerprint, nameof(InputFingerprint));
        Require(ProjectionFingerprint, nameof(ProjectionFingerprint));
    }

    private static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Required.", parameterName);
        }
    }
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
