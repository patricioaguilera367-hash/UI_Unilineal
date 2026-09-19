using System.Collections.ObjectModel;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Documents;

public enum ExportPreflightIssueCode
{
    UnknownLineStyle,
    UnknownTextStyle,
    MissingFont,
    DrawingProfileFingerprintMismatch
}

public sealed record ExportPreflightIssue(
    ExportPreflightIssueCode Code,
    string Message,
    int? SheetNumber = null,
    string? ElementId = null);

public sealed class ExportPreflightResult
{
    public ExportPreflightResult(IEnumerable<ExportPreflightIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = new ReadOnlyCollection<ExportPreflightIssue>(issues.ToArray());
    }

    public IReadOnlyList<ExportPreflightIssue> Issues { get; }

    public bool HasErrors => Issues.Count > 0;
}

public sealed class ExportPreflightOptions
{
    public ExportPreflightOptions(
        bool strictFonts,
        IEnumerable<string> availableFontFamilies)
    {
        ArgumentNullException.ThrowIfNull(availableFontFamilies);
        StrictFonts = strictFonts;
        AvailableFontFamilies = Array.AsReadOnly(
            availableFontFamilies
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public bool StrictFonts { get; }

    public IReadOnlyList<string> AvailableFontFamilies { get; }

    public static ExportPreflightOptions NonStrict { get; } =
        new(false, []);
}

public sealed class DocumentPreflight
{
    public ExportPreflightResult Validate(
        DrawingDocument document,
        ResolvedDrawingStyleSet styles,
        ExportPreflightOptions options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(options);

        var issues = new List<ExportPreflightIssue>();

        if (options.StrictFonts)
        {
            HashSet<string> available = new(
                options.AvailableFontFamilies,
                StringComparer.OrdinalIgnoreCase);

            foreach (string requiredFont in styles.RequiredFontFamilies)
            {
                if (!available.Contains(requiredFont))
                {
                    issues.Add(new ExportPreflightIssue(
                        ExportPreflightIssueCode.MissingFont,
                        $"Required font '{requiredFont}' is not available."));
                }
            }
        }

        foreach (DrawingSheet sheet in document.Sheets)
        {
            ValidateProfile(sheet, styles, issues);
            ValidateSceneStyles(sheet, styles, issues);
        }

        return new ExportPreflightResult(issues);
    }

    private static void ValidateProfile(
        DrawingSheet sheet,
        ResolvedDrawingStyleSet styles,
        ICollection<ExportPreflightIssue> issues)
    {
        DiagramSceneMetadata metadata = sheet.Scene.Metadata;
        if (!string.Equals(
                metadata.DrawingProfileId,
                styles.ProfileId,
                StringComparison.Ordinal) ||
            !string.Equals(
                metadata.DrawingProfileVersion,
                styles.ProfileVersion,
                StringComparison.Ordinal) ||
            !string.Equals(
                metadata.DrawingProfileFingerprint,
                styles.ProfileFingerprint,
                StringComparison.Ordinal))
        {
            issues.Add(new ExportPreflightIssue(
                ExportPreflightIssueCode.DrawingProfileFingerprintMismatch,
                "Scene drawing profile does not match the resolved print style set.",
                sheet.SheetNumber));
        }
    }

    private static void ValidateSceneStyles(
        DrawingSheet sheet,
        ResolvedDrawingStyleSet styles,
        ICollection<ExportPreflightIssue> issues)
    {
        foreach (SceneElement element in sheet.Scene.Elements)
        {
            switch (element)
            {
                case LineSceneElement line:
                    ValidateLineStyle(
                        sheet.SheetNumber,
                        line.Id.Value,
                        line.LineStyleId,
                        styles,
                        issues);
                    break;
                case PolylineSceneElement polyline:
                    ValidateLineStyle(
                        sheet.SheetNumber,
                        polyline.Id.Value,
                        polyline.LineStyleId,
                        styles,
                        issues);
                    break;
                case RectangleSceneElement rectangle:
                    ValidateLineStyle(
                        sheet.SheetNumber,
                        rectangle.Id.Value,
                        rectangle.LineStyleId,
                        styles,
                        issues);
                    break;
                case CircleSceneElement circle:
                    ValidateLineStyle(
                        sheet.SheetNumber,
                        circle.Id.Value,
                        circle.LineStyleId,
                        styles,
                        issues);
                    break;
                case PathSceneElement path:
                    ValidateLineStyle(
                        sheet.SheetNumber,
                        path.Id.Value,
                        path.LineStyleId,
                        styles,
                        issues);
                    break;
                case TextSceneElement text:
                    if (!styles.TryResolveText(text.TextStyleId, out _))
                    {
                        issues.Add(new ExportPreflightIssue(
                            ExportPreflightIssueCode.UnknownTextStyle,
                            $"Unknown text style '{text.TextStyleId}'.",
                            sheet.SheetNumber,
                            text.Id.Value));
                    }
                    break;
            }
        }

        foreach (SceneConnection connection in sheet.Scene.Connections)
        {
            ValidateLineStyle(
                sheet.SheetNumber,
                connection.Id.Value,
                connection.LineStyleId,
                styles,
                issues);
        }
    }

    private static void ValidateLineStyle(
        int sheetNumber,
        string elementId,
        string styleId,
        ResolvedDrawingStyleSet styles,
        ICollection<ExportPreflightIssue> issues)
    {
        if (!styles.TryResolveLine(styleId, out _))
        {
            issues.Add(new ExportPreflightIssue(
                ExportPreflightIssueCode.UnknownLineStyle,
                $"Unknown line style '{styleId}'.",
                sheetNumber,
                elementId));
        }
    }
}
