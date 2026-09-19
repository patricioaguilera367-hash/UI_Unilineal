using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Documents;

public sealed record DocumentCompositionRequest
{
    public DocumentCompositionRequest(
        string documentId,
        string revision,
        string projectName,
        string drawingTitle,
        string documentCode,
        DiagramScene scene)
    {
        DocumentId = Required(documentId, nameof(documentId));
        Revision = Required(revision, nameof(revision));
        ProjectName = Required(projectName, nameof(projectName));
        DrawingTitle = Required(drawingTitle, nameof(drawingTitle));
        DocumentCode = Required(documentCode, nameof(documentCode));
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public string DocumentId { get; }

    public string Revision { get; }

    public string ProjectName { get; }

    public string DrawingTitle { get; }

    public string DocumentCode { get; }

    public DiagramScene Scene { get; }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}

public sealed class DocumentComposer
{
    public DrawingDocument Compose(
        DocumentCompositionRequest request,
        DocumentCompositionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(policy);

        foreach (PaperSize paper in policy.EnumeratePapers())
        {
            MmSize area = policy.PrintableSceneArea(paper);
            double maximumFitScale = Math.Min(
                area.Width / request.Scene.Bounds.Width,
                area.Height / request.Scene.Bounds.Height);

            if (maximumFitScale <
                policy.MinimumLegibilityScale)
            {
                continue;
            }

            double scale = Math.Min(
                policy.PreferredScale,
                maximumFitScale);

            DrawingSheet sheet = CreateSheet(
                request,
                policy,
                paper,
                sheetNumber: 1,
                scale,
                request.Scene.Bounds,
                continuation: null);

            return new DrawingDocument(
                request.DocumentId,
                request.Revision,
                [sheet]);
        }

        PaperSize continuationPaper =
            policy.EnumeratePapers().Last();
        return ComposeContinuationSheets(
            request,
            policy,
            continuationPaper);
    }

    private static DrawingDocument ComposeContinuationSheets(
        DocumentCompositionRequest request,
        DocumentCompositionPolicy policy,
        PaperSize paper)
    {
        MmSize area = policy.PrintableSceneArea(paper);
        double scale = policy.MinimumLegibilityScale;
        double sourceWidthPerSheet = area.Width / scale;
        double sourceHeightPerSheet = area.Height / scale;

        int columns = (int)Math.Ceiling(
            request.Scene.Bounds.Width /
            sourceWidthPerSheet);
        int rows = (int)Math.Ceiling(
            request.Scene.Bounds.Height /
            sourceHeightPerSheet);
        int sheetCount = checked(columns * rows);

        var sheets = new List<DrawingSheet>(sheetCount);
        int sheetNumber = 1;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                double x =
                    request.Scene.Bounds.X +
                    (column * sourceWidthPerSheet);
                double y =
                    request.Scene.Bounds.Y +
                    (row * sourceHeightPerSheet);
                double width = Math.Min(
                    sourceWidthPerSheet,
                    request.Scene.Bounds.Right - x);
                double height = Math.Min(
                    sourceHeightPerSheet,
                    request.Scene.Bounds.Bottom - y);

                var viewBox = new MmRect(
                    x,
                    y,
                    width,
                    height);
                var continuation = new SheetContinuation(
                    sheetNumber,
                    sheetCount,
                    sheetNumber == 1
                        ? null
                        : sheetNumber - 1,
                    sheetNumber == sheetCount
                        ? null
                        : sheetNumber + 1);

                sheets.Add(
                    CreateSheet(
                        request,
                        policy,
                        paper,
                        sheetNumber,
                        scale,
                        viewBox,
                        continuation));
                sheetNumber++;
            }
        }

        return new DrawingDocument(
            request.DocumentId,
            request.Revision,
            sheets);
    }

    private static DrawingSheet CreateSheet(
        DocumentCompositionRequest request,
        DocumentCompositionPolicy policy,
        PaperSize paper,
        int sheetNumber,
        double scale,
        MmRect viewBox,
        SheetContinuation? continuation)
    {
        var sceneViewport = new MmRect(
            policy.Margins.LeftMm,
            policy.Margins.TopMm,
            viewBox.Width * scale,
            viewBox.Height * scale);
        string sheetLabel = continuation is null
            ? $"S{sheetNumber:00}"
            : $"S{sheetNumber:00}/{continuation.SequenceCount:00}";
        var titleBlock = new TitleBlock(
            request.ProjectName,
            request.DrawingTitle,
            sheetLabel,
            request.DocumentCode,
            request.Revision);

        return new DrawingSheet(
            sheetNumber,
            paper,
            scale,
            viewBox,
            sceneViewport,
            policy.Margins,
            titleBlock,
            request.Scene,
            continuation);
    }
}
