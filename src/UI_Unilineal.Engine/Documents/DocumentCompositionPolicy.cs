using System.Collections.ObjectModel;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Documents;

public sealed class DocumentCompositionPolicy
{
    public DocumentCompositionPolicy(
        PaperSize preferredPaper,
        IReadOnlyList<PaperSize> fallbackPapers,
        SheetMargins margins,
        double titleBlockHeightMm,
        double preferredScale,
        double minimumLegibilityScale)
    {
        PreferredPaper = preferredPaper ??
            throw new ArgumentNullException(nameof(preferredPaper));
        ArgumentNullException.ThrowIfNull(fallbackPapers);

        if (!double.IsFinite(titleBlockHeightMm) ||
            titleBlockHeightMm < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(titleBlockHeightMm));
        }

        if (!double.IsFinite(preferredScale) ||
            preferredScale <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preferredScale));
        }

        if (!double.IsFinite(minimumLegibilityScale) ||
            minimumLegibilityScale <= 0 ||
            minimumLegibilityScale > preferredScale)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumLegibilityScale));
        }

        PaperSize[] copiedFallbacks = fallbackPapers.ToArray();
        if (copiedFallbacks.Any(paper => paper is null))
        {
            throw new ArgumentException(
                "Fallback paper collection cannot contain null.",
                nameof(fallbackPapers));
        }

        Margins = margins;
        TitleBlockHeightMm = titleBlockHeightMm;
        PreferredScale = preferredScale;
        MinimumLegibilityScale = minimumLegibilityScale;
        FallbackPapers =
            new ReadOnlyCollection<PaperSize>(copiedFallbacks);

        foreach (PaperSize paper in EnumeratePapers())
        {
            ValidateUsableArea(paper);
        }
    }

    public PaperSize PreferredPaper { get; }

    public IReadOnlyList<PaperSize> FallbackPapers { get; }

    public SheetMargins Margins { get; }

    public double TitleBlockHeightMm { get; }

    public double PreferredScale { get; }

    public double MinimumLegibilityScale { get; }

    internal IEnumerable<PaperSize> EnumeratePapers()
    {
        yield return PreferredPaper;

        foreach (PaperSize paper in FallbackPapers)
        {
            yield return paper;
        }
    }

    internal MmSize PrintableSceneArea(PaperSize paper)
    {
        double width =
            paper.WidthMm -
            Margins.LeftMm -
            Margins.RightMm;
        double height =
            paper.HeightMm -
            Margins.TopMm -
            Margins.BottomMm -
            TitleBlockHeightMm;

        return new MmSize(width, height);
    }

    private void ValidateUsableArea(PaperSize paper)
    {
        double width =
            paper.WidthMm -
            Margins.LeftMm -
            Margins.RightMm;
        double height =
            paper.HeightMm -
            Margins.TopMm -
            Margins.BottomMm -
            TitleBlockHeightMm;

        if (!double.IsFinite(width) ||
            !double.IsFinite(height) ||
            width <= 0 ||
            height <= 0)
        {
            throw new ArgumentException(
                "Margins and title block must leave a positive scene area.");
        }
    }
}
