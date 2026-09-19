using System.Globalization;
using System.Text;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Documents;

namespace UI_Unilineal.Export.Svg;

public sealed class SvgExporter
{
    private static readonly Encoding Utf8NoBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public IReadOnlyList<SvgExportEntry> GetEntries(
        DrawingDocument document,
        SvgExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= new SvgExportOptions();

        return document.Sheets
            .OrderBy(sheet => sheet.SheetNumber)
            .Select(sheet => new SvgExportEntry(
                sheet.SheetNumber,
                $"{options.FileNamePrefix}-S{sheet.SheetNumber:00}.svg"))
            .ToArray();
    }

    public async ValueTask ExportSheetAsync(
        DrawingDocument document,
        int sheetNumber,
        ResolvedDrawingStyleSet styles,
        Stream destination,
        SvgExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(styles);
        ArgumentNullException.ThrowIfNull(destination);

        if (!destination.CanWrite)
        {
            throw new ArgumentException(
                "Destination stream must be writable.",
                nameof(destination));
        }

        options ??= new SvgExportOptions();
        cancellationToken.ThrowIfCancellationRequested();

        DrawingSheet sheet = document.Sheets
            .SingleOrDefault(value => value.SheetNumber == sheetNumber)
            ?? throw new ArgumentOutOfRangeException(
                nameof(sheetNumber),
                "Sheet number does not exist in the document.");

        ExportPreflightResult preflight =
            new DocumentPreflight().Validate(
                document,
                styles,
                options.Preflight);

        if (preflight.HasErrors)
        {
            string diagnostics = string.Join(
                "; ",
                preflight.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message}"));

            throw new InvalidOperationException(
                $"SVG export preflight failed. {diagnostics}");
        }

        string svg = BuildSheet(
            sheet,
            styles,
            cancellationToken);
        byte[] bytes = Utf8NoBom.GetBytes(svg);

        cancellationToken.ThrowIfCancellationRequested();
        await destination.WriteAsync(
            bytes.AsMemory(),
            cancellationToken);
    }

    private static string BuildSheet(
        DrawingSheet sheet,
        ResolvedDrawingStyleSet styles,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder(4096);

        output
            .Append("<svg xmlns=\"http://www.w3.org/2000/svg\"")
            .Append(" width=\"")
            .Append(Number(sheet.Paper.WidthMm))
            .Append("mm\" height=\"")
            .Append(Number(sheet.Paper.HeightMm))
            .Append("mm\" viewBox=\"0 0 ")
            .Append(Number(sheet.Paper.WidthMm))
            .Append(' ')
            .Append(Number(sheet.Paper.HeightMm))
            .Append("\" version=\"1.1\">\n");

        AppendDocumentMetadata(output, sheet);

        output
            .Append("  <svg x=\"")
            .Append(Number(sheet.SceneViewport.X))
            .Append("\" y=\"")
            .Append(Number(sheet.SceneViewport.Y))
            .Append("\" width=\"")
            .Append(Number(sheet.SceneViewport.Width))
            .Append("\" height=\"")
            .Append(Number(sheet.SceneViewport.Height))
            .Append("\" viewBox=\"")
            .Append(Number(sheet.ViewBox.X))
            .Append(' ')
            .Append(Number(sheet.ViewBox.Y))
            .Append(' ')
            .Append(Number(sheet.ViewBox.Width))
            .Append(' ')
            .Append(Number(sheet.ViewBox.Height))
            .Append("\" preserveAspectRatio=\"none\" overflow=\"hidden\">\n");

        foreach (SceneElement element in sheet.Scene.Elements
                     .Where(IsPrintable)
                     .OrderBy(value => value.Layer)
                     .ThenBy(value => value.ZIndex)
                     .ThenBy(value => value.Id.Value, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendElement(
                output,
                element,
                styles);
        }

        output.Append("  </svg>\n");
        output.Append("</svg>\n");
        return output.ToString();
    }

    private static void AppendDocumentMetadata(
        StringBuilder output,
        DrawingSheet sheet)
    {
        output
            .Append("  <metadata>")
            .Append(EscapeText(
                $"sheet={sheet.SheetNumber};profile={sheet.Scene.Metadata.DrawingProfileId};" +
                $"profileVersion={sheet.Scene.Metadata.DrawingProfileVersion};" +
                $"layout={sheet.Scene.Metadata.LayoutEngineVersion}"))
            .Append("</metadata>\n");
    }

    private static void AppendElement(
        StringBuilder output,
        SceneElement element,
        ResolvedDrawingStyleSet styles)
    {
        switch (element)
        {
            case LineSceneElement line:
                AppendLine(output, line, styles.ResolveLine(line.LineStyleId));
                break;

            case PolylineSceneElement polyline:
                AppendPolyline(
                    output,
                    polyline,
                    styles.ResolveLine(polyline.LineStyleId));
                break;

            case RectangleSceneElement rectangle:
                AppendRectangle(
                    output,
                    rectangle,
                    styles.ResolveLine(rectangle.LineStyleId));
                break;

            case CircleSceneElement circle:
                AppendCircle(
                    output,
                    circle,
                    styles.ResolveLine(circle.LineStyleId));
                break;

            case PathSceneElement path:
                AppendPath(
                    output,
                    path,
                    styles.ResolveLine(path.LineStyleId));
                break;

            case TextSceneElement text:
                AppendText(
                    output,
                    text,
                    styles.ResolveText(text.TextStyleId));
                break;

            case SymbolSceneElement:
            case GroupSceneElement:
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported scene element '{element.GetType().FullName}'.");
        }
    }

    private static void AppendLine(
        StringBuilder output,
        LineSceneElement line,
        ResolvedLineStyle style)
    {
        output
            .Append("    <line")
            .Append(Id(line))
            .Append(" x1=\"")
            .Append(Number(line.Start.X))
            .Append("\" y1=\"")
            .Append(Number(line.Start.Y))
            .Append("\" x2=\"")
            .Append(Number(line.End.X))
            .Append("\" y2=\"")
            .Append(Number(line.End.Y))
            .Append('\"');

        AppendLineStyle(output, style);
        output.Append(" />\n");
    }

    private static void AppendPolyline(
        StringBuilder output,
        PolylineSceneElement polyline,
        ResolvedLineStyle style)
    {
        output
            .Append("    <polyline")
            .Append(Id(polyline))
            .Append(" points=\"");

        for (int index = 0; index < polyline.Points.Count; index++)
        {
            if (index > 0)
            {
                output.Append(' ');
            }

            MmPoint point = polyline.Points[index];
            output
                .Append(Number(point.X))
                .Append(',')
                .Append(Number(point.Y));
        }

        output.Append('\"');
        AppendLineStyle(output, style);
        output.Append(" />\n");
    }

    private static void AppendRectangle(
        StringBuilder output,
        RectangleSceneElement rectangle,
        ResolvedLineStyle style)
    {
        output
            .Append("    <rect")
            .Append(Id(rectangle))
            .Append(" x=\"")
            .Append(Number(rectangle.Bounds.X))
            .Append("\" y=\"")
            .Append(Number(rectangle.Bounds.Y))
            .Append("\" width=\"")
            .Append(Number(rectangle.Bounds.Width))
            .Append("\" height=\"")
            .Append(Number(rectangle.Bounds.Height))
            .Append('\"');

        AppendLineStyle(output, style);
        output.Append(" />\n");
    }

    private static void AppendCircle(
        StringBuilder output,
        CircleSceneElement circle,
        ResolvedLineStyle style)
    {
        output
            .Append("    <circle")
            .Append(Id(circle))
            .Append(" cx=\"")
            .Append(Number(circle.Center.X))
            .Append("\" cy=\"")
            .Append(Number(circle.Center.Y))
            .Append("\" r=\"")
            .Append(Number(circle.RadiusMm))
            .Append('\"');

        AppendLineStyle(output, style);
        output.Append(" />\n");
    }

    private static void AppendPath(
        StringBuilder output,
        PathSceneElement path,
        ResolvedLineStyle style)
    {
        output
            .Append("    <path")
            .Append(Id(path))
            .Append(" d=\"")
            .Append(EscapeAttribute(path.Data))
            .Append('\"');

        AppendLineStyle(output, style);
        output.Append(" />\n");
    }

    private static void AppendText(
        StringBuilder output,
        TextSceneElement text,
        ResolvedTextStyle style)
    {
        output
            .Append("    <text")
            .Append(Id(text))
            .Append(" x=\"")
            .Append(Number(text.Bounds.X))
            .Append("\" y=\"")
            .Append(Number(text.Bounds.Y))
            .Append("\" dominant-baseline=\"hanging\" fill=\"black\" font-family=\"")
            .Append(EscapeAttribute(style.FontFamily))
            .Append("\" font-size=\"")
            .Append(Number(style.HeightMm))
            .Append("mm\" font-weight=\"")
            .Append(style.Bold ? "bold" : "normal")
            .Append("\">")
            .Append(EscapeText(text.Text))
            .Append("</text>\n");
    }

    private static void AppendLineStyle(
        StringBuilder output,
        ResolvedLineStyle style)
    {
        output
            .Append(" fill=\"none\" stroke=\"black\" stroke-width=\"")
            .Append(Number(style.WidthMm))
            .Append('\"');

        switch (style.Pattern)
        {
            case LinePattern.Solid:
                break;
            case LinePattern.Dashed:
                output.Append(" stroke-dasharray=\"4 2\"");
                break;
            case LinePattern.Dotted:
                output.Append(" stroke-dasharray=\"1 2\"");
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported line pattern '{style.Pattern}'.");
        }
    }

    private static bool IsPrintable(SceneElement element) =>
        element.Visibility is
            SceneVisibility.Print or
            SceneVisibility.Both;

    private static string Id(SceneElement element) =>
        $" data-scene-id=\"{EscapeAttribute(element.Id.Value)}\"";

    private static string Number(double value) =>
        value.ToString(
            "0.################",
            CultureInfo.InvariantCulture);

    private static string EscapeText(string value) =>
        SanitizeXmlCharacters(value)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    private static string SanitizeXmlCharacters(string value)
    {
        var output =
            new StringBuilder(value.Length);

        foreach (Rune rune in value.EnumerateRunes())
        {
            int codePoint =
                rune.Value;
            bool allowed =
                codePoint is 0x09 or 0x0A or 0x0D ||
                codePoint is >= 0x20 and <= 0xD7FF ||
                codePoint is >= 0xE000 and <= 0xFFFD ||
                codePoint is >= 0x10000 and <= 0x10FFFF;

            output.Append(
                allowed
                    ? rune.ToString()
                    : "\uFFFD");
        }

        return output.ToString();
    }

    private static string EscapeAttribute(string value) =>
        EscapeText(value)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
}
