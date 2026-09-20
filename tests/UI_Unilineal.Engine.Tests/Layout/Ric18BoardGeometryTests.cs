using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class Ric18BoardGeometryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Planner_OddCircuitCountKeepsMiddleAxisOnBoardCenter(
        int circuitCount)
    {
        LayoutProfile profile = Layout();
        Ric18BoardLayoutTokens tokens =
            Ric18BoardLayoutTokens.From(profile);

        Ric18BoardGeometry geometry =
            Plan(
                profile,
                tokens,
                circuitCount);

        Assert.Equal(
            geometry.CenterX,
            geometry.CircuitAxes[circuitCount / 2]);

        for (int index = 0; index < circuitCount / 2; index++)
        {
            double left =
                geometry.CenterX -
                geometry.CircuitAxes[index];
            double right =
                geometry.CircuitAxes[circuitCount - 1 - index] -
                geometry.CenterX;

            Assert.InRange(
                Math.Abs(left - right),
                0,
                1e-9);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Planner_EvenCircuitCountStraddlesBoardCenterSymmetrically(
        int circuitCount)
    {
        LayoutProfile profile = Layout();
        Ric18BoardLayoutTokens tokens =
            Ric18BoardLayoutTokens.From(profile);

        Ric18BoardGeometry geometry =
            Plan(
                profile,
                tokens,
                circuitCount);

        int leftIndex =
            (circuitCount / 2) - 1;
        int rightIndex =
            circuitCount / 2;

        Assert.True(
            geometry.CircuitAxes[leftIndex] <
            geometry.CenterX);
        Assert.True(
            geometry.CircuitAxes[rightIndex] >
            geometry.CenterX);
        Assert.InRange(
            Math.Abs(
                (geometry.CenterX -
                 geometry.CircuitAxes[leftIndex]) -
                (geometry.CircuitAxes[rightIndex] -
                 geometry.CenterX)),
            0,
            1e-9);
    }

    [Fact]
    public void Planner_BusAndHeaderRailsShareOneCoherentBoardEnvelope()
    {
        LayoutProfile profile = Layout();
        Ric18BoardLayoutTokens tokens =
            Ric18BoardLayoutTokens.From(profile);
        Ric18BoardGeometry geometry =
            Plan(
                profile,
                tokens,
                3);

        Assert.Equal(
            geometry.BoardLeft +
            tokens.BoardOuterPaddingMm,
            geometry.MainBusBounds.X);
        Assert.Equal(
            geometry.BoardWidth -
            (tokens.BoardOuterPaddingMm * 2.0),
            geometry.MainBusBounds.Width);
        Assert.True(
            geometry.ProtectiveEarthBounds.Right <
            geometry.CenterX);
        Assert.True(
            geometry.NeutralBounds.X >
            geometry.CenterX);
        Assert.True(
            geometry.MainBusY >
            geometry.ContentTop);
        Assert.True(
            geometry.BranchY >
            geometry.MainBusBounds.Bottom);
        Assert.True(
            geometry.ExternalDestinationY >
            geometry.BoardBottom);
    }

    [Fact]
    public void Tokens_AreExplicitUiLayoutDecisionsAndRemainCompact()
    {
        LayoutProfile profile = Layout();
        Ric18BoardLayoutTokens tokens =
            Ric18BoardLayoutTokens.From(profile);

        Assert.True(tokens.MinimumBoardWidthMm < 80);
        Assert.True(
            tokens.MainBusHeightMm <
            tokens.BoardHeaderHeightMm);
        Assert.True(
            tokens.ConnectionNodeRadiusMm <
            1.2);
        Assert.True(
            tokens.AnnotationClearanceMm >=
            profile.TextPaddingMm);
    }

    private static Ric18BoardGeometry Plan(
        LayoutProfile profile,
        Ric18BoardLayoutTokens tokens,
        int circuitCount) =>
        new Ric18BoardGeometryPlanner().Plan(
            profile,
            tokens,
            circuitCount,
            Enumerable.Repeat(
                    24.0,
                    circuitCount)
                .ToArray(),
            new MmSize(48, 14),
            new MmSize(18, 8),
            new MmSize(18, 8),
            incomingStackBottom: 26.5,
            mainProtectionStackHeight: 24,
            maximumBranchContentHeight: 48);

    private static LayoutProfile Layout() =>
        new(
            gridMm: 2.5,
            horizontalGapMm: 12,
            verticalGapMm: 12,
            branchGapMm: 8,
            routeClearanceMm: 3,
            textPaddingMm: 1,
            maxBoardDetailWidthMm: 260,
            continuationRowGapMm: 20,
            provenanceId: "APP:LAYOUT");
}
