using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Documents;

namespace UI_Unilineal.Engine.Tests.Documents;

public sealed class DocumentContractTests
{
    [Theory]
    [InlineData(PaperPreset.A0, 841d, 1189d)]
    [InlineData(PaperPreset.A1, 594d, 841d)]
    [InlineData(PaperPreset.A2, 420d, 594d)]
    [InlineData(PaperPreset.A3, 297d, 420d)]
    [InlineData(PaperPreset.A4, 210d, 297d)]
    public void Portrait_A_series_presets_use_explicit_millimetre_dimensions(
        PaperPreset preset,
        double expectedWidth,
        double expectedHeight)
    {
        PaperSize paper = PaperSize.FromPreset(preset, PageOrientation.Portrait);

        Assert.Equal(expectedWidth, paper.WidthMm);
        Assert.Equal(expectedHeight, paper.HeightMm);
        Assert.Equal(preset, paper.Preset);
        Assert.Equal(PageOrientation.Portrait, paper.Orientation);
    }

    [Fact]
    public void Landscape_orientation_swaps_physical_dimensions()
    {
        PaperSize paper = PaperSize.FromPreset(PaperPreset.A3, PageOrientation.Landscape);

        Assert.Equal(420d, paper.WidthMm);
        Assert.Equal(297d, paper.HeightMm);
    }

    [Fact]
    public void Custom_paper_requires_finite_positive_dimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PaperSize.Custom(0, 297, PageOrientation.Portrait));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PaperSize.Custom(210, double.NaN, PageOrientation.Portrait));
    }

    [Fact]
    public void Margins_reject_negative_or_non_finite_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SheetMargins(-1, 5, 5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SheetMargins(5, double.PositiveInfinity, 5, 5));
    }

    [Fact]
    public void Drawing_sheet_requires_positive_scale_and_viewports_inside_paper()
    {
        DiagramScene scene = CreateScene();
        PaperSize paper = PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Portrait);
        SheetMargins margins = new(10, 10, 10, 10);
        TitleBlock titleBlock = new("Project", "Single line", "S01", "DOC-001", "A");

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DrawingSheet(
                1,
                paper,
                0,
                new MmRect(10, 10, 100, 100),
                new MmRect(10, 10, 100, 100),
                margins,
                titleBlock,
                scene));

        Assert.Throws<ArgumentException>(
            () => new DrawingSheet(
                1,
                paper,
                1,
                new MmRect(10, 10, 100, 100),
                new MmRect(200, 10, 20, 20),
                margins,
                titleBlock,
                scene));
    }

    [Fact]
    public void Document_requires_one_based_contiguous_sheet_numbers()
    {
        DrawingSheet first = CreateSheet(1);
        DrawingSheet third = CreateSheet(3);

        Assert.Throws<ArgumentException>(
            () => new DrawingDocument("doc/demo", "rev-1", [first, third]));
    }

    [Fact]
    public void Document_copies_and_orders_valid_sheets_without_mutating_scene()
    {
        DrawingSheet second = CreateSheet(2);
        DrawingSheet first = CreateSheet(1);

        DrawingDocument document = new("doc/demo", "rev-1", [second, first]);

        Assert.Equal([1, 2], document.Sheets.Select(x => x.SheetNumber).ToArray());
        Assert.Equal("scene/demo", document.Sheets[0].Scene.Id.Value);
        Assert.Equal("rev-1", document.Revision);
    }

    private static DrawingSheet CreateSheet(int sheetNumber)
    {
        DiagramScene scene = CreateScene();
        return new DrawingSheet(
            sheetNumber,
            PaperSize.FromPreset(PaperPreset.A4, PageOrientation.Portrait),
            1,
            new MmRect(10, 10, 100, 100),
            new MmRect(10, 10, 100, 100),
            new SheetMargins(10, 10, 10, 10),
            new TitleBlock("Project", "Single line", $"S{sheetNumber:00}", "DOC-001", "A"),
            scene);
    }

    private static DiagramScene CreateScene() =>
        new(
            new SceneId("scene/demo"),
            DiagramSceneKind.Summary,
            new MmRect(0, 0, 100, 100),
            [],
            new DiagramSceneMetadata(
                "ric18",
                "1",
                "profile-fp",
                "layout-v1",
                "input-fp",
                "projection-fp"));
}
