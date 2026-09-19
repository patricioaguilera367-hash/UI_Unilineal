using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(
    typeof(UI_Unilineal.Rendering.Avalonia.Tests.Fixtures.RenderingTestAppBuilder))]

namespace UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;

public sealed class RenderingTestApplication : Application
{
}

public static class RenderingTestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<RenderingTestApplication>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
