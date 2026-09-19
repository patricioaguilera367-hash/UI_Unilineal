using System.Text;
using UI_Unilineal.Engine.Documents;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Playground.Export;
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Playground.ViewModels;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Playground;

public sealed class G8PlaygroundExportOrchestrationTests
{
    [Fact]
    public async Task Export_capability_false_disables_orchestration_and_preserves_destination()
    {
        using var temp = new TemporaryDirectory();
        string destination =
            temp.PathFor("drawing.pdf");
        await File.WriteAllTextAsync(
            destination,
            "ORIGINAL");

        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                HostCapabilities.ReadOnly);
        var orchestrator =
            new PlaygroundExportOrchestrator(
                viewModel);

        Assert.False(orchestrator.CanExport);

        bool exported =
            await orchestrator.ExportPdfAsync(
                destination);

        Assert.False(exported);
        Assert.Equal(
            "ORIGINAL",
            await File.ReadAllTextAsync(destination));
        Assert.Single(
            Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public async Task Preflight_failure_occurs_before_any_host_file_write()
    {
        using var temp = new TemporaryDirectory();
        string destination =
            temp.PathFor("drawing.pdf");
        await File.WriteAllTextAsync(
            destination,
            "ORIGINAL");

        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                ExportCapabilities());
        var orchestrator =
            new PlaygroundExportOrchestrator(
                viewModel);
        var strictMissingFonts =
            new ExportPreflightOptions(
                strictFonts: true,
                availableFontFamilies: []);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await orchestrator.ExportPdfAsync(
                    destination,
                    strictMissingFonts));

        Assert.Equal(
            "ORIGINAL",
            await File.ReadAllTextAsync(destination));
        Assert.Single(
            Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public async Task Successful_export_atomically_replaces_destination_with_pdf()
    {
        using var temp = new TemporaryDirectory();
        string destination =
            temp.PathFor("drawing.pdf");
        await File.WriteAllTextAsync(
            destination,
            "OLD");

        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                ExportCapabilities());
        var orchestrator =
            new PlaygroundExportOrchestrator(
                viewModel);

        Assert.True(orchestrator.CanExport);

        bool exported =
            await orchestrator.ExportPdfAsync(
                destination);

        Assert.True(exported);

        byte[] bytes =
            await File.ReadAllBytesAsync(
                destination);
        Assert.StartsWith(
            "%PDF-1.7",
            Encoding.ASCII.GetString(bytes));
        Assert.Single(
            Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public async Task Atomic_writer_failure_leaves_prior_destination_untouched()
    {
        using var temp = new TemporaryDirectory();
        string destination =
            temp.PathFor("drawing.pdf");
        await File.WriteAllTextAsync(
            destination,
            "ORIGINAL");

        var writer =
            new AtomicFileWriter();

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () =>
                await writer.WriteAsync(
                    destination,
                    async (stream, cancellationToken) =>
                    {
                        byte[] partial =
                            Encoding.ASCII.GetBytes(
                                "PARTIAL");
                        await stream.WriteAsync(
                            partial,
                            cancellationToken);
                        throw new InvalidOperationException(
                            "Injected failure.");
                    }));

        Assert.Equal(
            "ORIGINAL",
            await File.ReadAllTextAsync(destination));
        Assert.Single(
            Directory.EnumerateFiles(temp.Path));
    }

    [Fact]
    public async Task Atomic_writer_cancellation_leaves_prior_destination_untouched()
    {
        using var temp = new TemporaryDirectory();
        string destination =
            temp.PathFor("drawing.pdf");
        await File.WriteAllTextAsync(
            destination,
            "ORIGINAL");

        var writer =
            new AtomicFileWriter();
        using var cts =
            new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
                await writer.WriteAsync(
                    destination,
                    async (stream, cancellationToken) =>
                    {
                        byte[] partial =
                            Encoding.ASCII.GetBytes(
                                "PARTIAL");
                        await stream.WriteAsync(
                            partial,
                            cancellationToken);
                        cts.Cancel();
                        cancellationToken.ThrowIfCancellationRequested();
                    },
                    cts.Token));

        Assert.Equal(
            "ORIGINAL",
            await File.ReadAllTextAsync(destination));
        Assert.Single(
            Directory.EnumerateFiles(temp.Path));
    }

    private static HostCapabilities ExportCapabilities() =>
        new(
            CanEditLayout: false,
            CanEditElectrical: false,
            CanCreateCircuits: false,
            CanDeleteCircuits: false,
            CanEditProtection: false,
            CanExport: true,
            CanPersistLayout: false);

    private sealed class TemporaryDirectory :
        IDisposable
    {
        public TemporaryDirectory()
        {
            Path =
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "UI_Unilineal-G8-" +
                    Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string PathFor(string fileName) =>
            System.IO.Path.Combine(
                Path,
                fileName);

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(
                    Path,
                    recursive: true);
            }
        }
    }
}
