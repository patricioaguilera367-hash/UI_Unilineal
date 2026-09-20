using Avalonia.Headless.XUnit;
using UI_Unilineal.Playground.Views;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Playground;

public sealed class PlaygroundStartupTests
{
    [AvaloniaFact]
    public void MainWindow_CanConstructShowAndRemainVisible()
    {
        var window =
            new MainWindow();

        try
        {
            window.Show();

            Assert.True(
                window.IsVisible);
            Assert.NotNull(
                window.Content);
        }
        finally
        {
            window.Close();
        }
    }
}
