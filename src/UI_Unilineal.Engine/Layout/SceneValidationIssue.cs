namespace UI_Unilineal.Engine.Layout;

public enum SceneValidationSeverity
{
    Error,
    Warning
}

public enum SceneValidationMode
{
    Basic,
    Strict
}

public sealed record SceneValidationIssue(
    string Code,
    SceneValidationSeverity Severity,
    string Message,
    string? SceneId = null,
    string? Field = null);

public sealed class DiagramSceneValidationResult
{
    public DiagramSceneValidationResult(
        IEnumerable<SceneValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyList<SceneValidationIssue> Issues { get; }

    public bool HasErrors =>
        Issues.Any(issue => issue.Severity == SceneValidationSeverity.Error);
}

public static class SceneValidationCodes
{
    public const string DuplicateSceneId = "DUPLICATE_SCENE_ID";
    public const string InvalidBounds = "INVALID_BOUNDS";
    public const string MissingAnchor = "MISSING_ANCHOR";
    public const string OrphanConnection = "ORPHAN_CONNECTION";
    public const string ElementOutsideSceneBounds = "ELEMENT_OUTSIDE_SCENE_BOUNDS";
    public const string DuplicateAnchorId = "DUPLICATE_ANCHOR_ID";
    public const string AnchorOutsideElementBounds = "ANCHOR_OUTSIDE_ELEMENT_BOUNDS";
    public const string OrphanGroupChild = "ORPHAN_GROUP_CHILD";
    public const string StructuralBlockOverlap = "STRUCTURAL_BLOCK_OVERLAP";
    public const string RouteIntersectsStructuralBlock = "ROUTE_INTERSECTS_STRUCTURAL_BLOCK";
    public const string RouteIntersectsText = "ROUTE_INTERSECTS_TEXT";
}
