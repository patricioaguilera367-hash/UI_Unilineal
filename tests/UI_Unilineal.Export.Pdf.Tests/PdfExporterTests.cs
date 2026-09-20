using System.Text;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Documents;
using UI_Unilineal.Export.Pdf;

namespace UI_Unilineal.Export.Pdf.Tests;

public sealed class PdfExporterTests
{
    [Fact]
    public async Task Export_is_deterministic_multipage_and_uses_physical_media_boxes()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) = CreateDocument();
        var exporter = new PdfExporter();

        byte[] first = await Export(exporter, document, styles);
        byte[] second = await Export(exporter, document, styles);
        string pdf = Encoding.ASCII.GetString(first);

        Assert.Equal(first, second);
        Assert.StartsWith("%PDF-1.7", pdf, StringComparison.Ordinal);
        Assert.Contains("/Count 2", pdf, StringComparison.Ordinal);
        Assert.Contains(
            "/MediaBox [0 0 841.889764 595.275591]",
            pdf,
            StringComparison.Ordinal);
        Assert.Contains(
            "/MediaBox [0 0 595.275591 841.889764]",
            pdf,
            StringComparison.Ordinal);
        Assert.Contains("xref", pdf, StringComparison.Ordinal);
        Assert.Contains("trailer", pdf, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Supported_scene_primitives_are_pdf_vector_or_text_operators()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) = CreateDocument();

        string pdf = Encoding.ASCII.GetString(
            await Export(new PdfExporter(), document, styles));

        Assert.Contains(" m\n", pdf, StringComparison.Ordinal);
        Assert.Contains(" l\n", pdf, StringComparison.Ordinal);
        Assert.Contains(" re\n", pdf, StringComparison.Ordinal);
        Assert.Contains(" c\n", pdf, StringComparison.Ordinal);
        Assert.Contains("BT\n", pdf, StringComparison.Ordinal);
        Assert.Contains(" Tj\n", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/Subtype /Image", pdf, StringComparison.Ordinal);
        Assert.DoesNotContain("/XObject", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Solid_junction_circle_uses_fill_and_stroke_operator()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument(
                solidCircle: true);

        string pdf =
            Encoding.ASCII.GetString(
                await Export(
                    new PdfExporter(),
                    document,
                    styles));

        Assert.Contains(
            "0 g\nB\n",
            pdf,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pdf_text_escapes_delimiters_controls_and_non_ascii_deterministically()
    {
        const string text =
            "A(\\)\n\r\t\u0001ñ😀";
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument(
                text: text);

        byte[] first =
            await Export(
                new PdfExporter(),
                document,
                styles);
        byte[] second =
            await Export(
                new PdfExporter(),
                document,
                styles);
        string pdf =
            Encoding.ASCII.GetString(first);

        Assert.Equal(first, second);
        Assert.Contains(
            "A\\(\\\\\\)\\n\\r\\t???",
            pdf,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\u0001",
            pdf,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pre_cancelled_export_does_not_mutate_destination_stream()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument();
        byte[] original =
            Encoding.ASCII.GetBytes("ORIGINAL");
        var destination =
            new MemoryStream();
        await destination.WriteAsync(original);

        using var cts =
            new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await new PdfExporter().ExportAsync(
                    document,
                    styles,
                    destination,
                    cancellationToken: cts.Token));

        Assert.Equal(
            original,
            destination.ToArray());
    }

    [Fact]
    public async Task Preflight_failure_does_not_mutate_destination_stream()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument("UNKNOWN_STYLE");
        var destination = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new PdfExporter().ExportAsync(
                document,
                styles,
                destination));

        Assert.Equal(0, destination.Length);
    }

    private static async Task<byte[]> Export(
        PdfExporter exporter,
        DrawingDocument document,
        ResolvedDrawingStyleSet styles)
    {
        var stream = new MemoryStream();
        await exporter.ExportAsync(document, styles, stream);
        return stream.ToArray();
    }

    private static (
        DrawingDocument Document,
        ResolvedDrawingStyleSet Styles) CreateDocument(
            string lineStyleId = "POWER",
            string text = "Demo (PDF) \\ test",
            bool solidCircle = false)
    {
        var styles = new ResolvedDrawingStyleSet(
            "ric18",
            "1",
            "profile-fp",
            [
                new ResolvedLineStyle(
                    "POWER",
                    LineSemanticRole.Power,
                    0.35,
                    LinePattern.Solid)
            ],
            [
                new ResolvedTextStyle(
                    "TECH",
                    "Arial",
                    2.5,
                    false)
            ]);

        var elements = new SceneElement[]
        {
            new LineSceneElement(
                new SceneId("scene/demo/line"),
                new MmRect(10, 10, 20, 0.001),
                SceneLayer.Power,
                1,
                SceneVisibility.Both,
                null,
                null,
                new MmPoint(10, 10),
                new MmPoint(30, 10),
                lineStyleId),
            new PolylineSceneElement(
                new SceneId("scene/demo/polyline"),
                new MmRect(10, 20, 20, 10),
                SceneLayer.Power,
                2,
                SceneVisibility.Both,
                null,
                null,
                [new MmPoint(10, 20), new MmPoint(20, 20), new MmPoint(30, 30)],
                "POWER"),
            new RectangleSceneElement(
                new SceneId("scene/demo/rectangle"),
                new MmRect(40, 10, 10, 8),
                SceneLayer.Symbol,
                3,
                SceneVisibility.Both,
                null,
                null,
                "POWER"),
            new CircleSceneElement(
                new SceneId("scene/demo/circle"),
                new MmRect(55, 10, 10, 10),
                SceneLayer.Symbol,
                4,
                SceneVisibility.Both,
                null,
                solidCircle
                    ? new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["fillMode"] = "Solid"
                    }
                    : null,
                new MmPoint(60, 15),
                5,
                "POWER"),
            new PathSceneElement(
                new SceneId("scene/demo/path"),
                new MmRect(70, 10, 10, 10),
                SceneLayer.Symbol,
                5,
                SceneVisibility.Both,
                null,
                null,
                "M 70 10 L 80 20",
                "POWER"),
            new TextSceneElement(
                new SceneId("scene/demo/text"),
                new MmRect(10, 40, 40, 5),
                SceneLayer.Text,
                6,
                SceneVisibility.Both,
                null,
                null,
                text,
                "TECH")
        };

        var scene = new DiagramScene(
            new SceneId("scene/demo"),
            DiagramSceneKind.ProjectSummary,
            new MmRect(0, 0, 100, 80),
            elements,
            new DiagramSceneMetadata(
                styles.ProfileId,
                styles.ProfileVersion,
                styles.ProfileFingerprint,
                "layout-v1",
                "input-fp",
                "projection-fp"),
            [],
            []);

        DrawingSheet Sheet(
            int number,
            PageOrientation orientation) =>
            new(
                number,
                PaperSize.FromPreset(PaperPreset.A4, orientation),
                1,
                scene.Bounds,
                new MmRect(10, 10, 100, 80),
                new SheetMargins(10, 10, 10, 10),
                new TitleBlock(
                    "Project",
                    "Single line",
                    $"S{number:00}",
                    "DOC-001",
                    "A"),
                scene);

        return (
            new DrawingDocument(
                "doc/demo",
                "A",
                [
                    Sheet(1, PageOrientation.Landscape),
                    Sheet(2, PageOrientation.Portrait)
                ]),
            styles);
    }
}
