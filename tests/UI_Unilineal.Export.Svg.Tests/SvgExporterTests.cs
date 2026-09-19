using System.Text;
using System.Xml.Linq;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Documents;
using UI_Unilineal.Export.Svg;

namespace UI_Unilineal.Export.Svg.Tests;

public sealed class SvgExporterTests
{
    [Fact]
    public void Document_entries_are_ordered_and_named_per_sheet()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) = CreateDocument();

        IReadOnlyList<SvgExportEntry> entries =
            new SvgExporter().GetEntries(document, new SvgExportOptions("demo"));

        SvgExportEntry entry = Assert.Single(entries);
        Assert.Equal(1, entry.SheetNumber);
        Assert.Equal("demo-S01.svg", entry.FileName);
    }

    [Fact]
    public async Task Export_is_deterministic_physical_and_vector_only()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) = CreateDocument();
        var exporter = new SvgExporter();

        byte[] first = await Export(exporter, document, styles);
        byte[] second = await Export(exporter, document, styles);
        string svg = Encoding.UTF8.GetString(first);

        Assert.Equal(first, second);
        Assert.Contains("width=\"297mm\"", svg, StringComparison.Ordinal);
        Assert.Contains("height=\"210mm\"", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=\"0 0 297 210\"", svg, StringComparison.Ordinal);
        Assert.Contains("<line", svg, StringComparison.Ordinal);
        Assert.Contains("<polyline", svg, StringComparison.Ordinal);
        Assert.Contains("<rect", svg, StringComparison.Ordinal);
        Assert.Contains("<circle", svg, StringComparison.Ordinal);
        Assert.Contains("<path", svg, StringComparison.Ordinal);
        Assert.Contains("<text", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<image", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Project_text_is_xml_escaped_and_cannot_inject_markup()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument("A <script>alert(1)</script> & \"x\"");

        string svg = Encoding.UTF8.GetString(
            await Export(new SvgExporter(), document, styles));

        Assert.DoesNotContain("<script>", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", svg, StringComparison.Ordinal);
        Assert.Contains("&amp;", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unusual_unicode_and_xml_controls_are_sanitized_without_losing_valid_text()
    {
        const string text =
            "Tablero <TDA> & \"línea\" — ñ 😀\u0001";

        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument(text);

        string svg = Encoding.UTF8.GetString(
            await Export(
                new SvgExporter(),
                document,
                styles));

        XDocument xml =
            XDocument.Parse(svg);
        XNamespace ns =
            "http://www.w3.org/2000/svg";
        XElement textElement =
            Assert.Single(
                xml.Descendants(ns + "text"));

        Assert.Equal(
            "Tablero <TDA> & \"línea\" — ñ 😀�",
            textElement.Value);
        Assert.DoesNotContain(
            "\u0001",
            svg,
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
                await new SvgExporter().ExportSheetAsync(
                    document,
                    1,
                    styles,
                    destination,
                    cancellationToken: cts.Token));

        Assert.Equal(
            original,
            destination.ToArray());
    }

    [Fact]
    public async Task Interactive_only_elements_are_not_printed()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument(
                "Demo",
                new LineSceneElement(
                    new SceneId("scene/demo/interactive"),
                    new MmRect(1, 1, 10, 0.001),
                    SceneLayer.Interaction,
                    999,
                    SceneVisibility.Interactive,
                    null,
                    null,
                    new MmPoint(1, 1),
                    new MmPoint(11, 1),
                    "POWER"));

        string svg = Encoding.UTF8.GetString(
            await Export(new SvgExporter(), document, styles));

        Assert.DoesNotContain(
            "scene/demo/interactive",
            svg,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Preflight_failure_does_not_mutate_destination_stream()
    {
        (DrawingDocument document, ResolvedDrawingStyleSet styles) =
            CreateDocument(
                "Demo",
                new LineSceneElement(
                    new SceneId("scene/demo/bad"),
                    new MmRect(1, 1, 10, 0.001),
                    SceneLayer.Power,
                    1,
                    SceneVisibility.Both,
                    null,
                    null,
                    new MmPoint(1, 1),
                    new MmPoint(11, 1),
                    "UNKNOWN_STYLE"));
        var destination = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await new SvgExporter().ExportSheetAsync(
                document,
                1,
                styles,
                destination));

        Assert.Equal(0, destination.Length);
    }

    private static async Task<byte[]> Export(
        SvgExporter exporter,
        DrawingDocument document,
        ResolvedDrawingStyleSet styles)
    {
        var stream = new MemoryStream();
        await exporter.ExportSheetAsync(
            document,
            1,
            styles,
            stream);
        return stream.ToArray();
    }

    private static (
        DrawingDocument Document,
        ResolvedDrawingStyleSet Styles) CreateDocument(
            string text = "Demo",
            SceneElement? additional = null)
    {
        var profile = new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);

        var elements = new List<SceneElement>
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
                "POWER"),
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
                null,
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
                new MmRect(10, 40, 70, 5),
                SceneLayer.Text,
                6,
                SceneVisibility.Both,
                null,
                null,
                text,
                "TECH")
        };

        if (additional is not null)
        {
            elements.Add(additional);
        }

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

        var sheet = new DrawingSheet(
            1,
            PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Landscape),
            1,
            scene.Bounds,
            new MmRect(10, 10, 100, 80),
            new SheetMargins(10, 10, 10, 10),
            new TitleBlock("Project", "Single line", "S01", "DOC-001", "A"),
            scene);

        return (
            new DrawingDocument("doc/demo", "A", [sheet]),
            styles);
    }
}
