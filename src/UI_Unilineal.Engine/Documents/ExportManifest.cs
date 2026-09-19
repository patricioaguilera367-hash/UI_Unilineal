using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Documents;

public sealed class ExportManifest : IEquatable<ExportManifest>
{
    public ExportManifest(
        string projectUid,
        string inputFingerprint,
        string projectionFingerprint,
        IReadOnlyList<string> sceneFingerprints,
        string drawingProfileId,
        string drawingProfileVersion,
        string drawingProfileFingerprint,
        string layoutEngineVersion,
        string exporterId,
        string exporterVersion,
        string documentRevision,
        int sheetCount)
    {
        ProjectUid = projectUid;
        InputFingerprint = inputFingerprint;
        ProjectionFingerprint = projectionFingerprint;
        SceneFingerprints = Array.AsReadOnly(sceneFingerprints.ToArray());
        DrawingProfileId = drawingProfileId;
        DrawingProfileVersion = drawingProfileVersion;
        DrawingProfileFingerprint = drawingProfileFingerprint;
        LayoutEngineVersion = layoutEngineVersion;
        ExporterId = exporterId;
        ExporterVersion = exporterVersion;
        DocumentRevision = documentRevision;
        SheetCount = sheetCount;
    }

    public string ProjectUid { get; }

    public string InputFingerprint { get; }

    public string ProjectionFingerprint { get; }

    public IReadOnlyList<string> SceneFingerprints { get; }

    public string DrawingProfileId { get; }

    public string DrawingProfileVersion { get; }

    public string DrawingProfileFingerprint { get; }

    public string LayoutEngineVersion { get; }

    public string ExporterId { get; }

    public string ExporterVersion { get; }

    public string DocumentRevision { get; }

    public int SheetCount { get; }

    public bool Equals(ExportManifest? other) =>
        other is not null &&
        string.Equals(ProjectUid, other.ProjectUid, StringComparison.Ordinal) &&
        string.Equals(InputFingerprint, other.InputFingerprint, StringComparison.Ordinal) &&
        string.Equals(ProjectionFingerprint, other.ProjectionFingerprint, StringComparison.Ordinal) &&
        SceneFingerprints.SequenceEqual(other.SceneFingerprints, StringComparer.Ordinal) &&
        string.Equals(DrawingProfileId, other.DrawingProfileId, StringComparison.Ordinal) &&
        string.Equals(DrawingProfileVersion, other.DrawingProfileVersion, StringComparison.Ordinal) &&
        string.Equals(DrawingProfileFingerprint, other.DrawingProfileFingerprint, StringComparison.Ordinal) &&
        string.Equals(LayoutEngineVersion, other.LayoutEngineVersion, StringComparison.Ordinal) &&
        string.Equals(ExporterId, other.ExporterId, StringComparison.Ordinal) &&
        string.Equals(ExporterVersion, other.ExporterVersion, StringComparison.Ordinal) &&
        string.Equals(DocumentRevision, other.DocumentRevision, StringComparison.Ordinal) &&
        SheetCount == other.SheetCount;

    public override bool Equals(object? obj) =>
        obj is ExportManifest other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ProjectUid, StringComparer.Ordinal);
        hash.Add(InputFingerprint, StringComparer.Ordinal);
        hash.Add(ProjectionFingerprint, StringComparer.Ordinal);
        foreach (string fingerprint in SceneFingerprints)
        {
            hash.Add(fingerprint, StringComparer.Ordinal);
        }

        hash.Add(DrawingProfileId, StringComparer.Ordinal);
        hash.Add(DrawingProfileVersion, StringComparer.Ordinal);
        hash.Add(DrawingProfileFingerprint, StringComparer.Ordinal);
        hash.Add(LayoutEngineVersion, StringComparer.Ordinal);
        hash.Add(ExporterId, StringComparer.Ordinal);
        hash.Add(ExporterVersion, StringComparer.Ordinal);
        hash.Add(DocumentRevision, StringComparer.Ordinal);
        hash.Add(SheetCount);
        return hash.ToHashCode();
    }
}

public sealed class ExportManifestFactory
{
    public ExportManifest Create(
        string projectUid,
        DrawingDocument document,
        ResolvedDrawingStyleSet styles,
        string exporterId,
        string exporterVersion)
    {
        if (string.IsNullOrWhiteSpace(projectUid))
        {
            throw new ArgumentException("Required.", nameof(projectUid));
        }

        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(styles);

        if (string.IsNullOrWhiteSpace(exporterId))
        {
            throw new ArgumentException("Required.", nameof(exporterId));
        }

        if (string.IsNullOrWhiteSpace(exporterVersion))
        {
            throw new ArgumentException("Required.", nameof(exporterVersion));
        }

        var first = document.Sheets[0].Scene.Metadata;
        foreach (DrawingSheet sheet in document.Sheets)
        {
            var metadata = sheet.Scene.Metadata;
            if (!SameSemanticMetadata(first, metadata))
            {
                throw new InvalidOperationException(
                    "All sheets in one export manifest must share semantic/profile metadata.");
            }
        }

        string[] sceneFingerprints = document.Sheets
            .Select(sheet => DiagramSceneFingerprint.Compute(sheet.Scene))
            .ToArray();

        return new ExportManifest(
            projectUid,
            first.InputFingerprint,
            first.ProjectionFingerprint,
            sceneFingerprints,
            styles.ProfileId,
            styles.ProfileVersion,
            styles.ProfileFingerprint,
            first.LayoutEngineVersion,
            exporterId,
            exporterVersion,
            document.Revision,
            document.Sheets.Count);
    }

    private static bool SameSemanticMetadata(
        UI_Unilineal.Domain.Scene.DiagramSceneMetadata left,
        UI_Unilineal.Domain.Scene.DiagramSceneMetadata right) =>
        string.Equals(left.InputFingerprint, right.InputFingerprint, StringComparison.Ordinal) &&
        string.Equals(left.ProjectionFingerprint, right.ProjectionFingerprint, StringComparison.Ordinal) &&
        string.Equals(left.DrawingProfileId, right.DrawingProfileId, StringComparison.Ordinal) &&
        string.Equals(left.DrawingProfileVersion, right.DrawingProfileVersion, StringComparison.Ordinal) &&
        string.Equals(left.DrawingProfileFingerprint, right.DrawingProfileFingerprint, StringComparison.Ordinal) &&
        string.Equals(left.LayoutEngineVersion, right.LayoutEngineVersion, StringComparison.Ordinal);
}
