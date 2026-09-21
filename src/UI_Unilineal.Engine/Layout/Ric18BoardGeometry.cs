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
        double annotationClearanceMm,
        double auxiliaryRailDepartureMm,
        double auxiliaryLaneOffsetMm,
        double connectionNodeRadiusMm,
        double? auxiliaryAnchorApproachMm = null,
        double? circuitEgressInsetMm = null,
        double? auxiliaryFrameClearanceMm = null)
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
        ValidatePositive(auxiliaryRailDepartureMm, nameof(auxiliaryRailDepartureMm));
        ValidatePositive(auxiliaryLaneOffsetMm, nameof(auxiliaryLaneOffsetMm));
        ValidatePositive(connectionNodeRadiusMm, nameof(connectionNodeRadiusMm));

        double resolvedAuxiliaryAnchorApproachMm =
            auxiliaryAnchorApproachMm ??
            auxiliaryRailDepartureMm;
        ValidatePositive(
            resolvedAuxiliaryAnchorApproachMm,
            nameof(auxiliaryAnchorApproachMm));

        double resolvedAuxiliaryFrameClearanceMm =
            auxiliaryFrameClearanceMm ??
            auxiliaryLaneOffsetMm;
        ValidatePositive(
            resolvedAuxiliaryFrameClearanceMm,
            nameof(auxiliaryFrameClearanceMm));

        double resolvedCircuitEgressInsetMm =
            circuitEgressInsetMm ??
            Math.Max(
                resolvedAuxiliaryFrameClearanceMm,
                Math.Min(
                    connectionNodeRadiusMm,
                    boardBottomPaddingMm / 2.0));
        ValidatePositive(
            resolvedCircuitEgressInsetMm,
            nameof(circuitEgressInsetMm));

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
        AuxiliaryRailDepartureMm = auxiliaryRailDepartureMm;
        AuxiliaryLaneOffsetMm = auxiliaryLaneOffsetMm;
        AuxiliaryAnchorApproachMm =
            resolvedAuxiliaryAnchorApproachMm;
        AuxiliaryFrameClearanceMm =
            resolvedAuxiliaryFrameClearanceMm;
        CircuitEgressInsetMm =
            resolvedCircuitEgressInsetMm;
        ConnectionNodeRadiusMm = connectionNodeRadiusMm;
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

    public double AuxiliaryRailDepartureMm { get; }

    public double AuxiliaryLaneOffsetMm { get; }

    public double AuxiliaryAnchorApproachMm { get; }

    public double AuxiliaryFrameClearanceMm { get; }

    public double CircuitEgressInsetMm { get; }

    public double ConnectionNodeRadiusMm { get; }

    public static Ric18BoardLayoutTokens From(LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        double grid = profile.GridMm;

        double auxiliaryFrameClearanceMm =
            Math.Max(
                profile.RouteClearanceMm,
                grid);
        double circuitEgressInsetMm =
            auxiliaryFrameClearanceMm;
        double boardBottomPaddingMm =
            Math.Max(
                Math.Max(5.0, grid * 2.0),
                profile.RouteClearanceMm +
                circuitEgressInsetMm +
                grid);

        return new Ric18BoardLayoutTokens(
            boardOuterPaddingMm: Math.Max(5.0, grid * 2.0),
            boardHeaderHeightMm: Math.Max(14.0, grid * 4.0),
            elementGapMm: Math.Max(2.0, grid),
            minimumBoardWidthMm: Math.Max(56.0, grid * 20.0),
            minimumCircuitPitchMm: Math.Max(26.0, grid * 10.0),
            mainBusHeightMm: Math.Max(4.0, grid * 1.5),
            branchStubHeightMm: Math.Max(2.0, grid * 0.75),
            boardBottomPaddingMm: boardBottomPaddingMm,
            externalDestinationGapMm: Math.Max(4.0, grid * 1.5),
            annotationClearanceMm: Math.Max(2.0, profile.TextPaddingMm * 2.0),
            auxiliaryRailDepartureMm: Math.Max(profile.RouteClearanceMm, grid),
            auxiliaryLaneOffsetMm: Math.Max(
                profile.RouteClearanceMm + profile.TextPaddingMm,
                grid * 1.5),
            connectionNodeRadiusMm: Math.Max(0.75, grid * 0.3),
            auxiliaryAnchorApproachMm: Math.Max(
                profile.RouteClearanceMm,
                grid),
            circuitEgressInsetMm: circuitEgressInsetMm,
            auxiliaryFrameClearanceMm: auxiliaryFrameClearanceMm);
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

public sealed record Ric18CircuitExtent
{
    public Ric18CircuitExtent(
        double leftMm,
        double rightMm)
    {
        ValidateNonNegative(leftMm, nameof(leftMm));
        ValidateNonNegative(rightMm, nameof(rightMm));

        LeftMm = leftMm;
        RightMm = rightMm;
    }

    public double LeftMm { get; }

    public double RightMm { get; }

    private static void ValidateNonNegative(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
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
        IReadOnlyList<Ric18CircuitExtent> circuitExtents,
        MmSize mainBusMeasuredSize,
        MmSize protectiveEarthSize,
        MmSize neutralSize,
        double incomingStackBottom,
        double mainProtectionStackHeight,
        double centeredHeaderLeftExtentMm,
        double centeredHeaderRightExtentMm,
        double maximumBranchContentHeight)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(circuitExtents);

        if (circuitCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(circuitCount));
        }

        if (circuitExtents.Count != circuitCount)
        {
            throw new ArgumentException(
                "Circuit extent count must match circuit count.",
                nameof(circuitExtents));
        }

        ValidateNonNegative(
            centeredHeaderLeftExtentMm,
            nameof(centeredHeaderLeftExtentMm));
        ValidateNonNegative(
            centeredHeaderRightExtentMm,
            nameof(centeredHeaderRightExtentMm));

        double maximumLeftExtent =
            circuitExtents.Count == 0
                ? 0
                : circuitExtents.Max(extent => extent.LeftMm);
        double maximumRightExtent =
            circuitExtents.Count == 0
                ? 0
                : circuitExtents.Max(extent => extent.RightMm);

        double circuitPitch =
            Math.Max(
                tokens.MinimumCircuitPitchMm,
                maximumRightExtent +
                tokens.AnnotationClearanceMm +
                maximumLeftExtent);

        double circuitAxisSpan =
            circuitCount == 0
                ? 0
                : (circuitCount - 1) * circuitPitch;
        double symmetricCircuitExtent =
            Math.Max(
                maximumLeftExtent,
                maximumRightExtent);

        double branchAreaWidth =
            Math.Max(
                mainBusMeasuredSize.Width,
                circuitAxisSpan +
                (symmetricCircuitExtent * 2.0));

        double leftHeaderHalfWidth =
            tokens.BoardOuterPaddingMm +
            protectiveEarthSize.Width +
            tokens.AnnotationClearanceMm +
            centeredHeaderLeftExtentMm;
        double rightHeaderHalfWidth =
            tokens.BoardOuterPaddingMm +
            neutralSize.Width +
            tokens.AnnotationClearanceMm +
            centeredHeaderRightExtentMm;
        double headerRequiredWidth =
            2.0 *
            Math.Max(
                leftHeaderHalfWidth,
                rightHeaderHalfWidth);

        double boardWidth =
            Math.Max(
                tokens.MinimumBoardWidthMm,
                Math.Max(
                    branchAreaWidth +
                    (tokens.BoardOuterPaddingMm * 2.0),
                    headerRequiredWidth));

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
            double centeredIndex =
                index -
                ((circuitCount - 1) / 2.0);
            circuitAxes.Add(
                centerX +
                (centeredIndex * circuitPitch));
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

    private static void ValidateNonNegative(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
