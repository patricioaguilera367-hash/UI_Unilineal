using UI_Unilineal.Engine.Documents;
using UI_Unilineal.Export.Pdf;
using UI_Unilineal.Playground.ViewModels;

namespace UI_Unilineal.Playground.Export;

public sealed class PlaygroundExportOrchestrator
{
    private static readonly DocumentCompositionPolicy ExportPolicy =
        new(
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
            new SheetMargins(
                leftMm: 10,
                topMm: 10,
                rightMm: 10,
                bottomMm: 10),
            titleBlockHeightMm: 20,
            preferredScale: 1,
            minimumLegibilityScale: 0.5);

    private readonly SingleLineWorkspaceViewModel _workspace;
    private readonly AtomicFileWriter _fileWriter;

    public PlaygroundExportOrchestrator(
        SingleLineWorkspaceViewModel workspace,
        AtomicFileWriter? fileWriter = null)
    {
        _workspace =
            workspace ??
            throw new ArgumentNullException(nameof(workspace));
        _fileWriter =
            fileWriter ??
            new AtomicFileWriter();
    }

    public bool CanExport =>
        _workspace.Capabilities.CanExport;

    public async ValueTask<bool> ExportPdfAsync(
        string destinationPath,
        ExportPreflightOptions? preflight = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanExport)
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();

        ExportPreflightOptions effectivePreflight =
            preflight ??
            ExportPreflightOptions.NonStrict;

        PreparedExport prepared =
            Prepare(
                effectivePreflight);

        await _fileWriter.WriteAsync(
            destinationPath,
            async (stream, token) =>
            {
                await new PdfExporter().ExportAsync(
                    prepared.Document,
                    prepared.Styles,
                    stream,
                    new PdfExportOptions(
                        effectivePreflight),
                    token);
            },
            cancellationToken);

        return true;
    }

    private PreparedExport Prepare(
        ExportPreflightOptions preflight)
    {
        DrawingDocument document =
            new DocumentComposer().Compose(
                new DocumentCompositionRequest(
                    DocumentId(),
                    revision: "A",
                    projectName: "UI_Unilineal Playground",
                    drawingTitle: DrawingTitle(),
                    documentCode: "PLAYGROUND-G8",
                    _workspace.Scene),
                ExportPolicy);

        ResolvedDrawingStyleSet styles =
            new PrintStyleResolver().Resolve(
                _workspace.Profile);

        ExportPreflightResult result =
            new DocumentPreflight().Validate(
                document,
                styles,
                preflight);

        if (result.HasErrors)
        {
            string diagnostics =
                string.Join(
                    "; ",
                    result.Issues.Select(
                        issue =>
                            $"{issue.Code}: {issue.Message}"));

            throw new InvalidOperationException(
                $"Playground export preflight failed. {diagnostics}");
        }

        return new PreparedExport(
            document,
            styles);
    }

    private string DocumentId() =>
        _workspace.CurrentRoute.Kind ==
        PlaygroundRouteKind.ProjectSummary
            ? "playground/project-summary"
            : $"playground/board/{_workspace.CurrentRoute.BoardUid?.Value}";

    private string DrawingTitle() =>
        _workspace.CurrentRoute.Kind ==
        PlaygroundRouteKind.ProjectSummary
            ? "Project Summary"
            : $"Board {_workspace.CurrentRoute.BoardUid?.Value}";

    private sealed record PreparedExport(
        DrawingDocument Document,
        ResolvedDrawingStyleSet Styles);
}
