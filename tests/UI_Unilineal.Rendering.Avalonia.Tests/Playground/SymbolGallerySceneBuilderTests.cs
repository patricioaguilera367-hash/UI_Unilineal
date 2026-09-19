using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Playground.Fixtures;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Playground;

public sealed class SymbolGallerySceneBuilderTests
{
    [Fact]
    public void Build_DefaultGallery_HidesConnectionAnchorOverlay()
    {
        PlaygroundFixture fixture =
            PlaygroundFixtureFactory.Create();

        DiagramScene scene =
            SymbolGallerySceneBuilder.Build(
                fixture.Profile);

        Assert.DoesNotContain(
            scene.Elements,
            element =>
                element.Id.Value.Contains(
                    "/anchor-",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Build_WhenAnchorsEnabled_ShowsConnectionAnchorOverlay()
    {
        PlaygroundFixture fixture =
            PlaygroundFixtureFactory.Create();

        DiagramScene scene =
            SymbolGallerySceneBuilder.Build(
                fixture.Profile,
                showGrid: true,
                showBounds: true,
                showAnchors: true);

        Assert.Contains(
            scene.Elements,
            element =>
                element.Id.Value.Contains(
                    "/anchor-",
                    StringComparison.Ordinal));
    }
}
