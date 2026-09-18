using Avalonia;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Viewport;

public sealed class ViewportControllerTests
{
    [Fact]
    public void Transform_UsesPhysicalDipScaleAndRoundTripsScenePoint()
    {
        Assert.Equal(
            96.0 / 25.4,
            ViewportTransform.DipsPerMillimetre,
            12);

        var state = new ViewportState(
            zoom: 1.75,
            panDip: new Vector(41.25, -18.5),
            viewportDip: new Size(1200, 800));
        var source = new MmPoint(123.456, -17.25);

        Point dip =
            ViewportTransform.SceneMmToDip(source, state);
        MmPoint roundTrip =
            ViewportTransform.DipToSceneMm(dip, state);

        Assert.Equal(source.X, roundTrip.X, 9);
        Assert.Equal(source.Y, roundTrip.Y, 9);
    }

    [Fact]
    public void ZoomAt_PreservesScenePointUnderCursorAndClampsZoom()
    {
        var controller = new ViewportController();
        var state = new ViewportState(
            zoom: 1.0,
            panDip: new Vector(80, 55),
            viewportDip: new Size(1000, 700),
            minZoom: 0.25,
            maxZoom: 4.0);
        var cursor = new Point(731.5, 286.25);

        MmPoint before =
            ViewportTransform.DipToSceneMm(cursor, state);
        ViewportState zoomed =
            controller.ZoomAt(state, cursor, 2.5);
        MmPoint after =
            ViewportTransform.DipToSceneMm(cursor, zoomed);

        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
        Assert.Equal(2.5, zoomed.Zoom, 12);

        ViewportState maxed =
            controller.ZoomAt(zoomed, cursor, 100);
        ViewportState mined =
            controller.ZoomAt(maxed, cursor, 0.0001);

        Assert.Equal(4.0, maxed.Zoom, 12);
        Assert.Equal(0.25, mined.Zoom, 12);
    }

    [Fact]
    public void Pan_ChangesOnlyPanVector()
    {
        var controller = new ViewportController();
        var state = new ViewportState(
            zoom: 2.0,
            panDip: new Vector(10, 20),
            viewportDip: new Size(900, 600),
            minZoom: 0.1,
            maxZoom: 12.0);

        ViewportState result =
            controller.Pan(
                state,
                new Vector(-15, 32));

        Assert.Equal(new Vector(-5, 52), result.PanDip);
        Assert.Equal(state.Zoom, result.Zoom);
        Assert.Equal(state.ViewportDip, result.ViewportDip);
        Assert.Equal(state.MinZoom, result.MinZoom);
        Assert.Equal(state.MaxZoom, result.MaxZoom);
    }

    [Fact]
    public void FitScene_ContainsWholeBoundsInsideRequestedMargin()
    {
        var controller = new ViewportController();
        var state = new ViewportState(
            zoom: 1,
            panDip: default,
            viewportDip: new Size(1000, 600),
            minZoom: 0.05,
            maxZoom: 32);
        var scene = new MmRect(10, 20, 100, 50);

        ViewportState fitted =
            controller.FitScene(
                state,
                scene,
                marginDip: 24);
        Rect dip =
            ViewportTransform.SceneRectMmToDip(
                scene,
                fitted);

        Assert.True(dip.Left >= 24 - 1e-9);
        Assert.True(dip.Top >= 24 - 1e-9);
        Assert.True(dip.Right <= 1000 - 24 + 1e-9);
        Assert.True(dip.Bottom <= 600 - 24 + 1e-9);

        double expectedScale = Math.Min(
            (1000 - 48) / 100.0,
            (600 - 48) / 50.0);
        double expectedZoom =
            expectedScale /
            ViewportTransform.DipsPerMillimetre;

        Assert.Equal(expectedZoom, fitted.Zoom, 9);
    }

    [Fact]
    public void FitSelection_UsesSameGeometryContractAsFitScene()
    {
        var controller = new ViewportController();
        var state = new ViewportState(
            zoom: 3,
            panDip: new Vector(500, -100),
            viewportDip: new Size(800, 500));
        var selection = new MmRect(-20, 15, 40, 80);

        ViewportState fitted =
            controller.FitSelection(
                state,
                selection,
                marginDip: 30);
        Rect dip =
            ViewportTransform.SceneRectMmToDip(
                selection,
                fitted);

        Assert.True(dip.Left >= 30 - 1e-9);
        Assert.True(dip.Top >= 30 - 1e-9);
        Assert.True(dip.Right <= 770 + 1e-9);
        Assert.True(dip.Bottom <= 470 + 1e-9);
    }

    [Fact]
    public void ActualSizeAndCenterOn_PreservePhysicalScaleAndCenterTarget()
    {
        var controller = new ViewportController();
        var state = new ViewportState(
            zoom: 2.5,
            panDip: new Vector(123, -99),
            viewportDip: new Size(960, 640));
        var target = new MmPoint(42, 27);

        ViewportState actual =
            controller.ActualSize(state, target);
        Point centered =
            ViewportTransform.SceneMmToDip(
                target,
                actual);

        Assert.Equal(1.0, actual.Zoom, 12);
        Assert.Equal(480, centered.X, 9);
        Assert.Equal(320, centered.Y, 9);

        ViewportState recentered =
            controller.CenterOn(state, target);
        Point centeredAtExistingZoom =
            ViewportTransform.SceneMmToDip(
                target,
                recentered);

        Assert.Equal(state.Zoom, recentered.Zoom);
        Assert.Equal(480, centeredAtExistingZoom.X, 9);
        Assert.Equal(320, centeredAtExistingZoom.Y, 9);
    }

    [Fact]
    public void RectTransform_RoundTrips()
    {
        var state = new ViewportState(
            zoom: 0.75,
            panDip: new Vector(-17, 88),
            viewportDip: new Size(1024, 768));
        var source = new MmRect(-5.5, 8.25, 123.75, 44.5);

        Rect dip =
            ViewportTransform.SceneRectMmToDip(
                source,
                state);
        MmRect roundTrip =
            ViewportTransform.DipRectToSceneMm(
                dip,
                state);

        Assert.Equal(source.X, roundTrip.X, 9);
        Assert.Equal(source.Y, roundTrip.Y, 9);
        Assert.Equal(source.Width, roundTrip.Width, 9);
        Assert.Equal(source.Height, roundTrip.Height, 9);
    }
}
