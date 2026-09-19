using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Documents;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Export.Pdf;
using UI_Unilineal.Export.Svg;

namespace UI_Unilineal.Engine.Tests.Exports;

public sealed class ExportParityGoldenTests
{
    [Theory]
    [InlineData("minimal-summary.txt", "minimal", "summary")]
    [InlineData("minimal-detail.txt", "minimal", "detail")]
    [InlineData("nested-detail.txt", "nested", "detail")]
    public async Task Cross_format_evidence_matches_reviewed_golden(
        string fileName,
        string fixture,
        string view)
    {
        ExportCase exportCase = BuildCase(fixture, view);
        string actual = Normalize(
            await FormatEvidenceAsync(exportCase));

        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Golden",
            "Exports",
            fileName);

        Assert.True(
            File.Exists(path),
            $"Golden file does not exist: {path}{Environment.NewLine}" +
            $"ACTUAL:{Environment.NewLine}{actual}");

        string expected = Normalize(
            File.ReadAllText(path));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Shuffled_equivalent_input_preserves_cross_format_output()
    {
        SingleLineInput original =
            SemanticFixtureFactory.NestedBoards();
        SingleLineInput shuffled =
            ReverseCollections(original);

        ExportCase first = BuildCase(
            original,
            "detail");
        ExportCase second = BuildCase(
            shuffled,
            "detail");

        Assert.Equal(
            first.Manifest.SceneFingerprints,
            second.Manifest.SceneFingerprints);

        byte[] firstPdf = await ExportPdfAsync(first);
        byte[] secondPdf = await ExportPdfAsync(second);
        Assert.Equal(firstPdf, secondPdf);

        IReadOnlyList<byte[]> firstSvg =
            await ExportSvgAsync(first);
        IReadOnlyList<byte[]> secondSvg =
            await ExportSvgAsync(second);

        Assert.Equal(firstSvg.Count, secondSvg.Count);
        for (int index = 0; index < firstSvg.Count; index++)
        {
            Assert.Equal(firstSvg[index], secondSvg[index]);
        }
    }

    [Fact]
    public void Exporter_projects_remain_downstream_adapters()
    {
        string repositoryRoot = FindRepositoryRoot();

        foreach (string project in new[]
        {
            "UI_Unilineal.Export.Svg",
            "UI_Unilineal.Export.Pdf"
        })
        {
            string path = Path.Combine(
                repositoryRoot,
                "src",
                project,
                $"{project}.csproj");
            string content = File.ReadAllText(path);

            Assert.DoesNotContain(
                "Avalonia",
                content,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "ProyectoElectrico",
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                "UI_Unilineal.Domain",
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                "UI_Unilineal.Engine",
                content,
                StringComparison.Ordinal);
        }
    }

    private static ExportCase BuildCase(
        string fixture,
        string view)
    {
        SingleLineInput input = fixture == "minimal"
            ? SemanticFixtureFactory.Minimal()
            : SemanticFixtureFactory.NestedBoards();

        return BuildCase(input, view);
    }

    private static ExportCase BuildCase(
        SingleLineInput input,
        string view)
    {
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(input);
        Assert.True(projectionResult.Success);

        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(
                projectionResult.Projection);

        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader().LoadDirectory(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "ProfileData"));

        var layoutEngine =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics());

        SingleLineLayoutResult layoutResult =
            view == "summary"
                ? layoutEngine.LayoutSummary(
                    projection,
                    profile)
                : layoutEngine.LayoutBoardDetail(
                    projection,
                    new EntityUid("B1"),
                    profile);

        Assert.True(
            layoutResult.Success,
            layoutResult.Failure?.Message);

        DiagramScene scene =
            Assert.IsType<DiagramScene>(
                layoutResult.Scene);

        var policy = new DocumentCompositionPolicy(
            PaperSize.FromPreset(
                PaperPreset.A4,
                PageOrientation.Landscape),
            [
                PaperSize.FromPreset(
                    PaperPreset.A3,
                    PageOrientation.Landscape),
                PaperSize.FromPreset(
                    PaperPreset.A2,
                    PageOrientation.Landscape),
                PaperSize.FromPreset(
                    PaperPreset.A1,
                    PageOrientation.Landscape),
                PaperSize.FromPreset(
                    PaperPreset.A0,
                    PageOrientation.Landscape)
            ],
            new SheetMargins(10, 10, 10, 10),
            titleBlockHeightMm: 20,
            preferredScale: 1,
            minimumLegibilityScale: 0.5);

        DrawingDocument document =
            new DocumentComposer().Compose(
                new DocumentCompositionRequest(
                    $"doc/{input.Project.Uid}/{view}",
                    "A",
                    input.Project.Code,
                    view,
                    "G8-EVIDENCE",
                    scene),
                policy);

        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);

        ExportManifest manifest =
            new ExportManifestFactory().Create(
                input.Project.Uid.ToString(),
                document,
                styles,
                "cross-format",
                "1");

        return new ExportCase(
            document,
            styles,
            manifest);
    }

    private static async Task<string> FormatEvidenceAsync(
        ExportCase exportCase)
    {
        IReadOnlyList<byte[]> svgSheets =
            await ExportSvgAsync(exportCase);
        byte[] pdfBytes =
            await ExportPdfAsync(exportCase);

        var output = new StringBuilder();
        output.Append("DOCUMENT|")
            .Append(exportCase.Document.DocumentId)
            .Append('|')
            .Append(exportCase.Document.Revision)
            .Append("|sheets=")
            .Append(exportCase.Document.Sheets.Count)
            .Append('\n');

        ExportManifest manifest = exportCase.Manifest;
        output.Append("MANIFEST|input=")
            .Append(manifest.InputFingerprint)
            .Append("|projection=")
            .Append(manifest.ProjectionFingerprint)
            .Append("|profile=")
            .Append(manifest.DrawingProfileId)
            .Append('@')
            .Append(manifest.DrawingProfileVersion)
            .Append('|')
            .Append(manifest.DrawingProfileFingerprint)
            .Append("|layout=")
            .Append(manifest.LayoutEngineVersion)
            .Append("|scenes=")
            .AppendJoin(',', manifest.SceneFingerprints)
            .Append('\n');

        for (int index = 0;
             index < exportCase.Document.Sheets.Count;
             index++)
        {
            DrawingSheet sheet =
                exportCase.Document.Sheets[index];
            string svg =
                Encoding.UTF8.GetString(svgSheets[index]);
            XDocument xml = XDocument.Parse(svg);
            XElement root = Assert.IsType<XElement>(
                xml.Root);
            XNamespace ns =
                "http://www.w3.org/2000/svg";
            XElement scene =
                Assert.Single(
                    root.Elements(ns + "svg"));

            output.Append("SHEET|")
                .Append(sheet.SheetNumber)
                .Append("|paper=")
                .Append(Number(sheet.Paper.WidthMm))
                .Append('x')
                .Append(Number(sheet.Paper.HeightMm))
                .Append("|scale=")
                .Append(Number(sheet.Scale))
                .Append("|view=")
                .Append(Rect(sheet.ViewBox))
                .Append("|viewport=")
                .Append(Rect(sheet.SceneViewport))
                .Append("|scene=")
                .Append(manifest.SceneFingerprints[index])
                .Append('\n');

            output.Append("SVG|")
                .Append(sheet.SheetNumber)
                .Append("|width=")
                .Append(root.Attribute("width")?.Value)
                .Append("|height=")
                .Append(root.Attribute("height")?.Value)
                .Append("|viewBox=")
                .Append(root.Attribute("viewBox")?.Value)
                .Append("|sceneViewBox=")
                .Append(scene.Attribute("viewBox")?.Value)
                .Append('\n');
        }

        string pdf = Encoding.ASCII.GetString(pdfBytes);
        MatchCollection mediaBoxes = Regex.Matches(
            pdf,
            @"/MediaBox \[0 0 (?<width>[0-9.]+) (?<height>[0-9.]+)\]",
            RegexOptions.CultureInvariant);

        Assert.Equal(
            exportCase.Document.Sheets.Count,
            mediaBoxes.Count);

        output.Append("PDF|pages=")
            .Append(mediaBoxes.Count)
            .Append("|media=");

        for (int index = 0;
             index < mediaBoxes.Count;
             index++)
        {
            if (index > 0)
            {
                output.Append(',');
            }

            output
                .Append(mediaBoxes[index].Groups["width"].Value)
                .Append('x')
                .Append(mediaBoxes[index].Groups["height"].Value);
        }

        output.Append('\n');
        return output.ToString();
    }

    private static async Task<IReadOnlyList<byte[]>>
        ExportSvgAsync(
            ExportCase exportCase)
    {
        var exporter = new SvgExporter();
        var bytes = new List<byte[]>();

        foreach (DrawingSheet sheet in
                 exportCase.Document.Sheets)
        {
            var stream = new MemoryStream();
            await exporter.ExportSheetAsync(
                exportCase.Document,
                sheet.SheetNumber,
                exportCase.Styles,
                stream);
            bytes.Add(stream.ToArray());
        }

        return bytes;
    }

    private static async Task<byte[]> ExportPdfAsync(
        ExportCase exportCase)
    {
        var stream = new MemoryStream();
        await new PdfExporter().ExportAsync(
            exportCase.Document,
            exportCase.Styles,
            stream);
        return stream.ToArray();
    }

    private static SingleLineInput ReverseCollections(
        SingleLineInput input) =>
        new(
            input.Project,
            input.Sources.Reverse(),
            input.Boards.Reverse(),
            input.Buses.Reverse(),
            input.Circuits.Reverse(),
            input.SupplyConnections.Reverse(),
            input.Protections.Reverse(),
            input.Grounding.Reverse(),
            input.Results.Reverse(),
            input.Metadata);

    private static string Rect(MmRect rectangle) =>
        string.Join(
            ',',
            Number(rectangle.X),
            Number(rectangle.Y),
            Number(rectangle.Width),
            Number(rectangle.Height));

    private static string Number(double value) =>
        value.ToString(
            "0.######",
            CultureInfo.InvariantCulture);

    private static string Normalize(string value) =>
        value.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "UI_Unilineal.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate UI_Unilineal repository root.");
    }

    private sealed record ExportCase(
        DrawingDocument Document,
        ResolvedDrawingStyleSet Styles,
        ExportManifest Manifest);
}
