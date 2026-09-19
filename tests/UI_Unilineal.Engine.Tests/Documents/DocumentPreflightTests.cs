using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Documents;

namespace UI_Unilineal.Engine.Tests.Documents;

public sealed class DocumentPreflightTests
{
    [Fact]
    public void Resolver_preserves_profile_style_semantics_and_required_fonts()
    {
        RIC18DrawingProfile profile = LoadProfile();

        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);

        ResolvedLineStyle power = styles.ResolveLine("POWER");
        ResolvedTextStyle tech = styles.ResolveText("TECH");

        Assert.Equal(0.35d, power.WidthMm);
        Assert.Equal(LinePattern.Solid, power.Pattern);
        Assert.Equal("Arial", tech.FontFamily);
        Assert.Equal(2.5d, tech.HeightMm);
        Assert.False(tech.Bold);
        Assert.Equal(["Arial"], styles.RequiredFontFamilies);
        Assert.Equal(
            DrawingProfileFingerprint.Compute(profile),
            styles.ProfileFingerprint);
    }

    [Fact]
    public void Preflight_rejects_unknown_scene_style_ids()
    {
        RIC18DrawingProfile profile = LoadProfile();
        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);
        DrawingDocument document = CreateDocument(
            styles,
            [
                CreateLine("UNKNOWN_LINE_STYLE"),
                CreateText("TECH")
            ]);

        ExportPreflightResult result =
            new DocumentPreflight().Validate(
                document,
                styles,
                ExportPreflightOptions.NonStrict);

        Assert.True(result.HasErrors);
        ExportPreflightIssue issue = Assert.Single(
            result.Issues,
            value => value.Code == ExportPreflightIssueCode.UnknownLineStyle);
        Assert.Equal("scene/demo/line", issue.ElementId);
        Assert.Equal(1, issue.SheetNumber);
    }

    [Fact]
    public void Strict_preflight_rejects_missing_required_fonts()
    {
        RIC18DrawingProfile profile = LoadProfile();
        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);
        DrawingDocument document = CreateDocument(
            styles,
            [
                CreateLine("POWER"),
                CreateText("TECH")
            ]);

        ExportPreflightResult result =
            new DocumentPreflight().Validate(
                document,
                styles,
                new ExportPreflightOptions(
                    strictFonts: true,
                    availableFontFamilies: []));

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code == ExportPreflightIssueCode.MissingFont &&
                issue.Message.Contains("Arial", StringComparison.Ordinal));
    }

    [Fact]
    public void Strict_preflight_accepts_declared_available_fonts()
    {
        RIC18DrawingProfile profile = LoadProfile();
        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);
        DrawingDocument document = CreateDocument(
            styles,
            [
                CreateLine("POWER"),
                CreateText("TECH")
            ]);

        ExportPreflightResult result =
            new DocumentPreflight().Validate(
                document,
                styles,
                new ExportPreflightOptions(
                    strictFonts: true,
                    availableFontFamilies: ["Arial"]));

        Assert.False(
            result.HasErrors,
            string.Join(Environment.NewLine, result.Issues));
    }

    [Fact]
    public void Preflight_rejects_style_set_from_different_profile_fingerprint()
    {
        RIC18DrawingProfile profile = LoadProfile();
        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);
        DrawingDocument document = CreateDocument(
            styles,
            [CreateLine("POWER")],
            profileFingerprint: "DIFFERENT");

        ExportPreflightResult result =
            new DocumentPreflight().Validate(
                document,
                styles,
                ExportPreflightOptions.NonStrict);

        Assert.Contains(
            result.Issues,
            issue =>
                issue.Code ==
                ExportPreflightIssueCode.DrawingProfileFingerprintMismatch);
    }

    [Fact]
    public void Export_manifest_is_deterministic_and_excludes_binary_noise()
    {
        RIC18DrawingProfile profile = LoadProfile();
        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(profile);
        DrawingDocument document = CreateDocument(
            styles,
            [
                CreateLine("POWER"),
                CreateText("TECH")
            ]);
        var factory = new ExportManifestFactory();

        ExportManifest first = factory.Create(
            "project-uid-001",
            document,
            styles,
            "svg",
            "1.0.0");
        ExportManifest second = factory.Create(
            "project-uid-001",
            document,
            styles,
            "svg",
            "1.0.0");

        Assert.Equal(first, second);
        Assert.Equal("project-uid-001", first.ProjectUid);
        Assert.Equal("input-fp", first.InputFingerprint);
        Assert.Equal("projection-fp", first.ProjectionFingerprint);
        Assert.Equal(styles.ProfileFingerprint, first.DrawingProfileFingerprint);
        Assert.Equal(styles.ProfileId, first.DrawingProfileId);
        Assert.Equal(styles.ProfileVersion, first.DrawingProfileVersion);
        Assert.Equal("layout-v1", first.LayoutEngineVersion);
        Assert.Equal("svg", first.ExporterId);
        Assert.Equal("1.0.0", first.ExporterVersion);
        Assert.Equal("A", first.DocumentRevision);
        Assert.Equal(1, first.SheetCount);
        Assert.Single(first.SceneFingerprints);

        string[] propertyNames = typeof(ExportManifest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("FileHash", StringComparison.OrdinalIgnoreCase));
    }

    private static RIC18DrawingProfile LoadProfile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));

    private static DrawingDocument CreateDocument(
        ResolvedDrawingStyleSet styles,
        IEnumerable<SceneElement> elements,
        string? profileFingerprint = null)
    {
        var scene = new DiagramScene(
            new SceneId("scene/demo"),
            DiagramSceneKind.ProjectSummary,
            new MmRect(0, 0, 100, 80),
            elements,
            new DiagramSceneMetadata(
                styles.ProfileId,
                styles.ProfileVersion,
                profileFingerprint ?? styles.ProfileFingerprint,
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

        return new DrawingDocument("doc/demo", "A", [sheet]);
    }

    private static LineSceneElement CreateLine(string styleId) =>
        new(
            new SceneId("scene/demo/line"),
            new MmRect(10, 10, 20, 0.001),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            null,
            new MmPoint(10, 10),
            new MmPoint(30, 10),
            styleId);

    private static TextSceneElement CreateText(string styleId) =>
        new(
            new SceneId("scene/demo/text"),
            new MmRect(10, 20, 20, 5),
            SceneLayer.Text,
            20,
            SceneVisibility.Both,
            null,
            null,
            "Demo",
            styleId);
}
