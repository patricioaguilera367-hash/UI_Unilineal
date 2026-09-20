using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Layout;

/// <summary>
/// Numeric choices owned by UI_Unilineal for the RIC18 visual grammar.
/// These values are implementation/layout decisions, not dimensions defined
/// by RIC 18 and not measurements copied from the hand-traced DXF.
/// </summary>
public sealed record Ric18BoardLayoutTokens
{
    public Ric18BoardLayoutTokens(
        double boardOuterPaddingMm,
        double boardHeaderHeightMm,
        double elementGapMm,
        double minimumBoardWidthMm,
        double minimumCircuitPitchMm,
        double mainBusHeightMm,
        double branchStubHeightMm,
        double boardBottomPaddingMm,
        double externalDestinationGapMm,
        double annotationClearanceMm)
    {
        ValidatePositive(boardOuterPaddingMm, nameof(boardOuterPaddingMm));
        ValidatePositive(boardHeaderHeightMm, nameof(boardHeaderHeightMm));
        ValidatePositive(elementGapMm, nameof(elementGapMm));
        ValidatePositive(minimumBoardWidthMm, nameof(minimumBoardWidthMm));
        ValidatePositive(minimumCircuitPitchMm, nameof(minimumCircuitPitchMm));
        ValidatePositive(mainBusHeightMm, nameof(mainBusHeightMm));
        ValidatePositive(branchStubHeightMm, nameof(branchStubHeightMm));
        ValidatePositive(boardBottomPaddingMm, nameof(boardBottomPaddingMm));
        ValidatePositive(externalDestinationGapMm, nameof(externalDestinationGapMm));
        ValidatePositive(annotationClearanceMm, nameof(annotationClearanceMm));

        BoardOuterPaddingMm = boardOuterPaddingMm;
        BoardHeaderHeightMm = boardHeaderHeightMm;
        ElementGapMm = elementGapMm;
        MinimumBoardWidthMm = minimumBoardWidthMm;
        MinimumCircuitPitchMm = minimumCircuitPitchMm;
        MainBusHeightMm = mainBusHeightMm;
        BranchStubHeightMm = branchStubHeightMm;
        BoardBottomPaddingMm = boardBottomPaddingMm;
        ExternalDestinationGapMm = externalDestinationGapMm;
        AnnotationClearanceMm = annotationClearanceMm;
    }

    public double BoardOuterPaddingMm { get; }

    public double BoardHeaderHeightMm { get; }

    public double ElementGapMm { get; }

    public double MinimumBoardWidthMm { get; }

    public double MinimumCircuitPitchMm { get; }

    public double MainBusHeightMm { get; }

    public double BranchStubHeightMm { get; }

    public double BoardBottomPaddingMm { get; }

    public double ExternalDestinationGapMm { get; }

    public double AnnotationClearanceMm { get; }

    public static Ric18BoardLayoutTokens From(LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        double grid = profile.GridMm;

        return new Ric18BoardLayoutTokens(
            boardOuterPaddingMm: Math.Max(5.0, grid * 2.0),
            boardHeaderHeightMm: Math.Max(10.0, grid * 4.0),
            elementGapMm: Math.Max(2.0, grid),
            minimumBoardWidthMm: Math.Max(56.0, grid * 20.0),
            minimumCircuitPitchMm: Math.Max(26.0, grid * 10.0),
            mainBusHeightMm: Math.Max(4.0, grid * 1.5),
            branchStubHeightMm: Math.Max(2.0, grid * 0.75),
            boardBottomPaddingMm: Math.Max(5.0, grid * 2.0),
            externalDestinationGapMm: Math.Max(4.0, grid * 1.5),
            annotationClearanceMm: Math.Max(2.0, profile.TextPaddingMm * 2.0));
    }

    private static void ValidatePositive(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public sealed record Ric18BoardGeometry(
    double BoardLeft,
    double BoardTop,
    double BoardWidth,
    double CenterX,
    double ContentTop,
    double MainBusY,
    double BranchY,
    double BranchContentY,
    double BoardBottom,
    double ExternalDestinationY,
    double CircuitPitch,
    MmRect MainBusBounds,
    MmRect ProtectiveEarthBounds,
    MmRect NeutralBounds,
    IReadOnlyList<double> CircuitAxes);

public sealed class Ric18BoardGeometryPlanner
{
    public Ric18BoardGeometry Plan(
        LayoutProfile profile,
        Ric18BoardLayoutTokens tokens,
        int circuitCount,
        IReadOnlyList<double> requiredCircuitWidthsMm,
        MmSize mainBusMeasuredSize,
        MmSize protectiveEarthSize,
        MmSize neutralSize,
        double incomingStackBottom,
        double mainProtectionStackHeight,
        double maximumBranchContentHeight)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(requiredCircuitWidthsMm);

        if (circuitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(circuitCount));
        }

        if (requiredCircuitWidthsMm.Count != circuitCount)
        {
            throw new ArgumentException(
                "Circuit width count must match circuit count.",
                nameof(requiredCircuitWidthsMm));
        }

        double widestCircuit =
            requiredCircuitWidthsMm.Count == 0
                ? 0
                : requiredCircuitWidthsMm.Max();

        double circuitPitch =
            Math.Max(
                tokens.MinimumCircuitPitchMm,
                widestCircuit +
                (tokens.AnnotationClearanceMm * 2.0));

        double minimumBranchAreaWidth =
            circuitCount == 0
                ? mainBusMeasuredSize.Width
                : circuitCount * circuitPitch;

        double branchAreaWidth =
            Math.Max(
                mainBusMeasuredSize.Width,
                minimumBranchAreaWidth);

        double boardWidth =
            Math.Max(
                tokens.MinimumBoardWidthMm,
                branchAreaWidth +
                (tokens.BoardOuterPaddingMm * 2.0));

        double boardLeft = profile.GridMm;
        double boardTop =
            incomingStackBottom +
            tokens.ElementGapMm;
        double centerX =
            boardLeft +
            (boardWidth / 2.0);
        double contentTop =
            boardTop +
            tokens.BoardHeaderHeightMm;
        double branchAreaLeft =
            boardLeft +
            tokens.BoardOuterPaddingMm;
        double actualBranchAreaWidth =
            boardWidth -
            (tokens.BoardOuterPaddingMm * 2.0);

        var circuitAxes =
            new List<double>(circuitCount);

        for (int index = 0; index < circuitCount; index++)
        {
            double axis =
                branchAreaLeft +
                (actualBranchAreaWidth *
                 (index + 0.5) /
                 circuitCount);
            circuitAxes.Add(axis);
        }

        double headerAccessoryBottom =
            contentTop +
            Math.Max(
                protectiveEarthSize.Height,
                neutralSize.Height);
        double mainProtectionBottom =
            contentTop +
            mainProtectionStackHeight;
        double mainBusY =
            Math.Max(
                headerAccessoryBottom,
                mainProtectionBottom) +
            tokens.ElementGapMm;

        var busBounds =
            new MmRect(
                branchAreaLeft,
                mainBusY,
                actualBranchAreaWidth,
                tokens.MainBusHeightMm);

        double branchY =
            busBounds.Bottom +
            tokens.ElementGapMm;
        double branchContentY =
            branchY +
            tokens.BranchStubHeightMm +
            tokens.ElementGapMm;
        double boardBottom =
            branchContentY +
            maximumBranchContentHeight +
            tokens.BoardBottomPaddingMm;
        double externalDestinationY =
            boardBottom +
            tokens.ExternalDestinationGapMm;

        var peBounds =
            new MmRect(
                branchAreaLeft,
                contentTop,
                protectiveEarthSize.Width,
                protectiveEarthSize.Height);
        var neutralBounds =
            new MmRect(
                boardLeft +
                boardWidth -
                tokens.BoardOuterPaddingMm -
                neutralSize.Width,
                contentTop,
                neutralSize.Width,
                neutralSize.Height);

        return new Ric18BoardGeometry(
            boardLeft,
            boardTop,
            boardWidth,
            centerX,
            contentTop,
            mainBusY,
            branchY,
            branchContentY,
            boardBottom,
            externalDestinationY,
            circuitPitch,
            busBounds,
            peBounds,
            neutralBounds,
            circuitAxes.AsReadOnly());
    }
}
