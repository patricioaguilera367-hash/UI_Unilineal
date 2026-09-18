namespace UI_Unilineal.Engine.Composition;

public enum ProfileValidationSeverity
{
    Error,
    Warning
}

public sealed record ProfileValidationIssue(
    string Code,
    ProfileValidationSeverity Severity,
    string Message,
    string? EntityId = null,
    string? Field = null);

public sealed class DrawingProfileValidationResult
{
    public DrawingProfileValidationResult(
        IEnumerable<ProfileValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyList<ProfileValidationIssue> Issues { get; }

    public bool HasErrors =>
        Issues.Any(issue => issue.Severity == ProfileValidationSeverity.Error);
}

public static class ProfileValidationCodes
{
    public const string InvalidVersion = "INVALID_PROFILE_VERSION";
    public const string DuplicateProvenanceId = "DUPLICATE_PROVENANCE_ID";
    public const string DuplicateSymbolId = "DUPLICATE_SYMBOL_ID";
    public const string DuplicateBlockId = "DUPLICATE_BLOCK_ID";
    public const string DuplicateLineStyleId = "DUPLICATE_LINE_STYLE_ID";
    public const string DuplicateTextStyleId = "DUPLICATE_TEXT_STYLE_ID";
    public const string MissingProvenance = "MISSING_PROVENANCE";
    public const string RicProvenanceMissingCitation = "RIC_PROVENANCE_MISSING_CITATION";
    public const string MissingLineStyle = "MISSING_LINE_STYLE";
    public const string MissingTextStyle = "MISSING_TEXT_STYLE";
    public const string AnchorOutsideBounds = "ANCHOR_OUTSIDE_BOUNDS";
    public const string MissingSymbol = "MISSING_SYMBOL";
    public const string MissingLabelSlot = "MISSING_LABEL_SLOT";
    public const string InvalidPrimitiveGeometry = "INVALID_PRIMITIVE_GEOMETRY";
}
