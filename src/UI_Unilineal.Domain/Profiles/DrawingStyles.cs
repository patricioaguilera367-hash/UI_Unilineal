namespace UI_Unilineal.Domain.Profiles;

public enum LineSemanticRole
{
    Power,
    Bus,
    Ground,
    Reference,
    Annotation,
    Boundary,
    AlternateSupply
}

public enum LinePattern
{
    Solid,
    Dashed,
    DashDot,
    Dotted
}

public sealed record LineStyleDefinition
{
    public LineStyleDefinition(
        string id,
        LineSemanticRole role,
        double widthMm,
        LinePattern pattern,
        string provenanceId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Line style ID is required.", nameof(id));
        }

        if (!double.IsFinite(widthMm) || widthMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMm));
        }

        if (string.IsNullOrWhiteSpace(provenanceId))
        {
            throw new ArgumentException("Provenance ID is required.", nameof(provenanceId));
        }

        Id = id;
        Role = role;
        WidthMm = widthMm;
        Pattern = pattern;
        ProvenanceId = provenanceId;
    }

    public string Id { get; }

    public LineSemanticRole Role { get; }

    public double WidthMm { get; }

    public LinePattern Pattern { get; }

    public string ProvenanceId { get; }
}

public sealed record TextStyleDefinition
{
    public TextStyleDefinition(
        string id,
        string fontFamily,
        double heightMm,
        bool bold,
        string provenanceId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Text style ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(fontFamily))
        {
            throw new ArgumentException("Font family is required.", nameof(fontFamily));
        }

        if (!double.IsFinite(heightMm) || heightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMm));
        }

        if (string.IsNullOrWhiteSpace(provenanceId))
        {
            throw new ArgumentException("Provenance ID is required.", nameof(provenanceId));
        }

        Id = id;
        FontFamily = fontFamily;
        HeightMm = heightMm;
        Bold = bold;
        ProvenanceId = provenanceId;
    }

    public string Id { get; }

    public string FontFamily { get; }

    public double HeightMm { get; }

    public bool Bold { get; }

    public string ProvenanceId { get; }
}

public sealed record LayoutProfile
{
    public LayoutProfile(
        double gridMm,
        double horizontalGapMm,
        double verticalGapMm,
        double branchGapMm,
        double routeClearanceMm,
        double textPaddingMm,
        double maxBoardDetailWidthMm,
        double continuationRowGapMm,
        string provenanceId)
    {
        ValidatePositive(gridMm, nameof(gridMm));
        ValidatePositive(horizontalGapMm, nameof(horizontalGapMm));
        ValidatePositive(verticalGapMm, nameof(verticalGapMm));
        ValidatePositive(branchGapMm, nameof(branchGapMm));
        ValidatePositive(routeClearanceMm, nameof(routeClearanceMm));
        ValidatePositive(textPaddingMm, nameof(textPaddingMm));
        ValidatePositive(maxBoardDetailWidthMm, nameof(maxBoardDetailWidthMm));
        ValidatePositive(continuationRowGapMm, nameof(continuationRowGapMm));

        if (string.IsNullOrWhiteSpace(provenanceId))
        {
            throw new ArgumentException("Provenance ID is required.", nameof(provenanceId));
        }

        GridMm = gridMm;
        HorizontalGapMm = horizontalGapMm;
        VerticalGapMm = verticalGapMm;
        BranchGapMm = branchGapMm;
        RouteClearanceMm = routeClearanceMm;
        TextPaddingMm = textPaddingMm;
        MaxBoardDetailWidthMm = maxBoardDetailWidthMm;
        ContinuationRowGapMm = continuationRowGapMm;
        ProvenanceId = provenanceId;
    }

    public double GridMm { get; }

    public double HorizontalGapMm { get; }

    public double VerticalGapMm { get; }

    public double BranchGapMm { get; }

    public double RouteClearanceMm { get; }

    public double TextPaddingMm { get; }

    public double MaxBoardDetailWidthMm { get; }

    public double ContinuationRowGapMm { get; }

    public string ProvenanceId { get; }

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
