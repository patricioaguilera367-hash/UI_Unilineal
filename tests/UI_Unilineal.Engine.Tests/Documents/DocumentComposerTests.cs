using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Documents;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Documents;

public sealed class DocumentComposerTests
{
    [Fact]
    public void Simple_scene_uses_preferred_sheet_and_scale()
    {
        DiagramScene scene = CreateScene(100, 80);
        var composer = new DocumentComposer();
        DocumentCompositionPolicy policy = Policy(
            PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Landscape),
            [],
            preferredScale: 1,
            minimumLegibilityScale: 0.5);

        DrawingDocument document = composer.Compose(Request(scene), policy);

        DrawingSheet sheet = Assert.Single(document.Sheets);
        Assert.Equal(PaperPreset.A4, sheet.Paper.Preset);
        Assert.Equal(PageOrientation.Landscape, sheet.Paper.Orientation);
        Assert.Equal(1d, sheet.Scale);
        Assert.Equal(scene.Bounds, sheet.ViewBox);
        Assert.Equal(new MmRect(10, 10, 100, 80), sheet.SceneViewport);
        Assert.Null(sheet.Continuation);
    }

    [Fact]
    public void Larger_fallback_is_selected_before_crossing_minimum_legibility()
    {
        DiagramScene scene = CreateScene(300, 200);
        var composer = new DocumentComposer();
        DocumentCompositionPolicy policy = Policy(
            PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Portrait),
            [PaperSize.FromPreset(PaperPreset.A3, PageOrientation.Landscape)],
            preferredScale: 1,
            minimumLegibilityScale: 0.75);

        DrawingDocument document = composer.Compose(Request(scene), policy);

        DrawingSheet sheet = Assert.Single(document.Sheets);
        Assert.Equal(PaperPreset.A3, sheet.Paper.Preset);
        Assert.Equal(PageOrientation.Landscape, sheet.Paper.Orientation);
        Assert.Equal(1d, sheet.Scale);
    }

    [Fact]
    public void Oversized_scene_creates_deterministic_row_major_continuation_sheets()
    {
        DiagramScene scene = CreateScene(1000, 500);
        var composer = new DocumentComposer();
        DocumentCompositionPolicy policy = Policy(
            PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Landscape),
            [],
            preferredScale: 1,
            minimumLegibilityScale: 0.5);

        DrawingDocument document = composer.Compose(Request(scene), policy);

        Assert.Equal(4, document.Sheets.Count);
        Assert.Equal(
            [
                new MmRect(0, 0, 554, 340),
                new MmRect(554, 0, 446, 340),
                new MmRect(0, 340, 554, 160),
                new MmRect(554, 340, 446, 160)
            ],
            document.Sheets.Select(sheet => sheet.ViewBox).ToArray());

        for (int index = 0; index < document.Sheets.Count; index++)
        {
            DrawingSheet sheet = document.Sheets[index];
            Assert.Equal(0.5d, sheet.Scale);
            Assert.NotNull(sheet.Continuation);
            Assert.Equal(index + 1, sheet.Continuation!.SequenceIndex);
            Assert.Equal(4, sheet.Continuation.SequenceCount);
            Assert.Equal(index == 0 ? null : index, sheet.Continuation.PreviousSheetNumber);
            Assert.Equal(index == 3 ? null : index + 2, sheet.Continuation.NextSheetNumber);
        }
    }

    [Fact]
    public void Composition_is_independent_of_equivalent_scene_element_order()
    {
        DiagramScene first = CreateScene(
            200,
            120,
            [
                CreateLine("scene/demo/line/a", 10, 10, 20, 10),
                CreateLine("scene/demo/line/b", 40, 30, 50, 30)
            ]);
        DiagramScene second = CreateScene(
            200,
            120,
            [
                CreateLine("scene/demo/line/b", 40, 30, 50, 30),
                CreateLine("scene/demo/line/a", 10, 10, 20, 10)
            ]);
        var composer = new DocumentComposer();
        DocumentCompositionPolicy policy = Policy(
            PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Landscape),
            [],
            preferredScale: 1,
            minimumLegibilityScale: 0.5);

        DrawingDocument left = composer.Compose(Request(first), policy);
        DrawingDocument right = composer.Compose(Request(second), policy);

        Assert.Equal(
            left.Sheets.Select(PhysicalSignature),
            right.Sheets.Select(PhysicalSignature));
    }

    [Fact]
    public void Composition_does_not_mutate_source_scene_fingerprint()
    {
        DiagramScene scene = CreateScene(100, 80);
        string before = DiagramSceneFingerprint.Compute(scene);
        var composer = new DocumentComposer();

        _ = composer.Compose(
            Request(scene),
            Policy(
                PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Landscape),
                [],
                preferredScale: 1,
                minimumLegibilityScale: 0.5));

        Assert.Equal(before, DiagramSceneFingerprint.Compute(scene));
    }

    private static DocumentCompositionPolicy Policy(
        PaperSize preferredPaper,
        IReadOnlyList<PaperSize> fallbackPapers,
        double preferredScale,
        double minimumLegibilityScale) =>
        new(
            preferredPaper,
            fallbackPapers,
            new SheetMargins(10, 10, 10, 10),
            titleBlockHeightMm: 20,
            preferredScale,
            minimumLegibilityScale);

    private static DocumentCompositionRequest Request(DiagramScene scene) =>
        new(
            "doc/demo",
            "A",
            "Project",
            "Single line",
            "DOC-001",
            scene);

    private static DiagramScene CreateScene(
        double width,
        double height,
        IEnumerable<SceneElement>? elements = null) =>
        new(
            new SceneId("scene/demo"),
            DiagramSceneKind.ProjectSummary,
            new MmRect(0, 0, width, height),
            elements ?? [],
            new DiagramSceneMetadata(
                "ric18",
                "1",
                "profile-fp",
                "layout-v1",
                "input-fp",
                "projection-fp"),
            [],
            []);

    private static LineSceneElement CreateLine(
        string id,
        double x1,
        double y1,
        double x2,
        double y2) =>
        new(
            new SceneId(id),
            new MmRect(
                Math.Min(x1, x2),
                Math.Min(y1, y2),
                Math.Max(Math.Abs(x2 - x1), 0.001),
                Math.Max(Math.Abs(y2 - y1), 0.001)),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            null,
            new MmPoint(x1, y1),
            new MmPoint(x2, y2),
            "line.power");

    private static string PhysicalSignature(DrawingSheet sheet) =>
        string.Join(
            "|",
            sheet.SheetNumber,
            sheet.Paper.Preset,
            sheet.Paper.Orientation,
            sheet.Paper.WidthMm,
            sheet.Paper.HeightMm,
            sheet.Scale,
            sheet.ViewBox,
            sheet.SceneViewport,
            sheet.Continuation?.SequenceIndex,
            sheet.Continuation?.SequenceCount);
}
