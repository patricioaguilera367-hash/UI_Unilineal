using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Documents;

public sealed record SheetContinuation
{
    public SheetContinuation(
        int sequenceIndex,
        int sequenceCount,
        int? previousSheetNumber,
        int? nextSheetNumber)
    {
        if (sequenceCount <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceCount));
        }

        if (sequenceIndex <= 0 ||
            sequenceIndex > sequenceCount)
        {
            throw new ArgumentOutOfRangeException(nameof(sequenceIndex));
        }

        ValidateNeighbor(
            previousSheetNumber,
            sequenceCount,
            nameof(previousSheetNumber));
        ValidateNeighbor(
            nextSheetNumber,
            sequenceCount,
            nameof(nextSheetNumber));

        SequenceIndex = sequenceIndex;
        SequenceCount = sequenceCount;
        PreviousSheetNumber = previousSheetNumber;
        NextSheetNumber = nextSheetNumber;
    }

    public int SequenceIndex { get; }

    public int SequenceCount { get; }

    public int? PreviousSheetNumber { get; }

    public int? NextSheetNumber { get; }

    private static void ValidateNeighbor(
        int? value,
        int sequenceCount,
        string parameterName)
    {
        if (value is <= 0 ||
            value > sequenceCount)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public sealed class DrawingSheet
{
    public DrawingSheet(
        int sheetNumber,
        PaperSize paper,
        double scale,
        MmRect viewBox,
        MmRect sceneViewport,
        SheetMargins margins,
        TitleBlock titleBlock,
        DiagramScene scene,
        SheetContinuation? continuation = null)
    {
        if (sheetNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sheetNumber));
        }

        if (!double.IsFinite(scale) || scale <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        Paper = paper ?? throw new ArgumentNullException(nameof(paper));
        TitleBlock = titleBlock ?? throw new ArgumentNullException(nameof(titleBlock));
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));

        ValidateMargins(paper, margins);
        ValidateSceneViewport(paper, margins, sceneViewport);

        if (continuation is not null &&
            continuation.SequenceIndex != sheetNumber)
        {
            throw new ArgumentException(
                "Continuation sequence must match the sheet number.",
                nameof(continuation));
        }

        SheetNumber = sheetNumber;
        Scale = scale;
        ViewBox = viewBox;
        SceneViewport = sceneViewport;
        Margins = margins;
        Continuation = continuation;
    }

    public int SheetNumber { get; }

    public PaperSize Paper { get; }

    public double Scale { get; }

    public MmRect ViewBox { get; }

    public MmRect SceneViewport { get; }

    public SheetMargins Margins { get; }

    public TitleBlock TitleBlock { get; }

    public DiagramScene Scene { get; }

    public SheetContinuation? Continuation { get; }

    private static void ValidateMargins(
        PaperSize paper,
        SheetMargins margins)
    {
        if (margins.LeftMm + margins.RightMm >= paper.WidthMm ||
            margins.TopMm + margins.BottomMm >= paper.HeightMm)
        {
            throw new ArgumentException(
                "Margins must leave a positive printable area.",
                nameof(margins));
        }
    }

    private static void ValidateSceneViewport(
        PaperSize paper,
        SheetMargins margins,
        MmRect sceneViewport)
    {
        double printableRight = paper.WidthMm - margins.RightMm;
        double printableBottom = paper.HeightMm - margins.BottomMm;

        if (sceneViewport.X < margins.LeftMm ||
            sceneViewport.Y < margins.TopMm ||
            sceneViewport.Right > printableRight ||
            sceneViewport.Bottom > printableBottom)
        {
            throw new ArgumentException(
                "Scene viewport must remain inside the printable area.",
                nameof(sceneViewport));
        }
    }
}
