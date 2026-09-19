using Avalonia.Headless.XUnit;
using Avalonia.Media;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Rendering.Avalonia.Rendering;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Rendering;

public sealed class AvaloniaRenderResourcesTests
{
    [Fact]
    public void Constructor_ExposesExactProfileFingerprint()
    {
        RIC18DrawingProfile profile = Profile();

        var resources = new AvaloniaRenderResources(
            profile,
            InteractiveThemeKind.Light);

        Assert.Equal(
            DrawingProfileFingerprint.Compute(profile),
            resources.ProfileFingerprint);
    }

    [Fact]
    public void ResolvePen_UsesProfileWidthAndPattern()
    {
        var resources = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Light);

        Pen power = resources.ResolvePen("POWER");
        Pen reference = resources.ResolvePen("REFERENCE");

        Assert.Equal(0.35, power.Thickness, 12);
        Assert.Null(power.DashStyle);
        Assert.NotNull(reference.DashStyle);
    }

    [Fact]
    public void ResolveTypeface_UsesProfileFamilyAndWeight()
    {
        var resources = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Light);

        Typeface tech = resources.ResolveTypeface("TECH");
        Typeface title = resources.ResolveTypeface("TITLE");

        Assert.Contains(
            "Arial",
            tech.FontFamily.Name,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(FontWeight.Normal, tech.Weight);
        Assert.Equal(FontWeight.Bold, title.Weight);
    }

    [Theory]
    [InlineData("MISSING_LINE", "pen")]
    [InlineData("MISSING_TEXT", "typeface")]
    [InlineData("MISSING_SYMBOL", "symbol")]
    public void UnknownTechnicalProfileId_ThrowsKeyNotFound(
        string id,
        string operation)
    {
        var resources = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Light);

        Assert.Throws<KeyNotFoundException>(
            () =>
            {
                switch (operation)
                {
                    case "pen":
                        _ = resources.ResolvePen(id);
                        break;

                    case "typeface":
                        _ = resources.ResolveTypeface(id);
                        break;

                    case "symbol":
                        _ = resources.ResolveSymbolGeometry(id);
                        break;

                    default:
                        throw new InvalidOperationException();
                }
            });
    }

    [AvaloniaFact]
    public void ResolveSymbolGeometry_CacheKeyIncludesVariant()
    {
        var resources = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Light);

        Geometry first =
            resources.ResolveSymbolGeometry(
                "BREAKER");
        Geometry repeated =
            resources.ResolveSymbolGeometry(
                "BREAKER");
        Geometry selected =
            resources.ResolveSymbolGeometry(
                "BREAKER",
                "selected");

        Assert.Same(first, repeated);
        Assert.NotSame(first, selected);
        Assert.True(first.Bounds.Width > 0);
        Assert.True(first.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public void SymbolCache_IsBoundedAndEvictsOldestEntry()
    {
        var resources = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Light,
            maxCachedSymbols: 2,
            maxCachedTextEntries: 8);

        Geometry original =
            resources.ResolveSymbolGeometry(
                "SOURCE_UTILITY");

        _ = resources.ResolveSymbolGeometry("BREAKER");
        _ = resources.ResolveSymbolGeometry("RCD");

        Geometry afterEviction =
            resources.ResolveSymbolGeometry(
                "SOURCE_UTILITY");

        Assert.NotSame(original, afterEviction);
    }

    [Fact]
    public void ThemeAndStatusResources_ReturnConcreteBrushes()
    {
        var light = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Light);
        var dark = new AvaloniaRenderResources(
            Profile(),
            InteractiveThemeKind.Dark);

        Assert.NotNull(light.ResolveTextBrush("TECH"));
        Assert.NotNull(dark.ResolveTextBrush("TECH"));
        Assert.NotNull(light.ResolveStatusBrush("Warning"));
        Assert.NotNull(light.ResolveStatusBrush("Error"));
        Assert.NotNull(light.ResolveStatusBrush("Ok"));
    }

    private static RIC18DrawingProfile Profile()
    {
        string repositoryRoot = FindRepositoryRoot();

        return new Ric18DrawingProfileLoader()
            .LoadDirectory(
                Path.Combine(
                    repositoryRoot,
                    "data",
                    "ric18",
                    "v1"));
    }

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
}
