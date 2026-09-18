using Avalonia;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Rendering.Avalonia.Viewport;

public sealed class ViewportController
{
    public ViewportState ZoomAt(
        ViewportState state,
        Point cursorDip,
        double zoomFactor)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!double.IsFinite(zoomFactor) || zoomFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomFactor));
        }

        MmPoint scenePoint =
            ViewportTransform.DipToSceneMm(
                cursorDip,
                state);
        double zoom = Math.Clamp(
            state.Zoom * zoomFactor,
            state.MinZoom,
            state.MaxZoom);
        double scale =
            ViewportTransform.DipsPerMillimetre * zoom;
        var pan = new Vector(
            cursorDip.X - (scenePoint.X * scale),
            cursorDip.Y - (scenePoint.Y * scale));

        return new ViewportState(
            zoom,
            pan,
            state.ViewportDip,
            state.MinZoom,
            state.MaxZoom);
    }

    public ViewportState Pan(
        ViewportState state,
        Vector deltaDip)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!double.IsFinite(deltaDip.X) ||
            !double.IsFinite(deltaDip.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaDip));
        }

        return new ViewportState(
            state.Zoom,
            state.PanDip + deltaDip,
            state.ViewportDip,
            state.MinZoom,
            state.MaxZoom);
    }

    public ViewportState FitScene(
        ViewportState state,
        MmRect sceneBounds,
        double marginDip = 24) =>
        Fit(state, sceneBounds, marginDip);

    public ViewportState FitSelection(
        ViewportState state,
        MmRect selectionBounds,
        double marginDip = 24) =>
        Fit(state, selectionBounds, marginDip);

    public ViewportState ActualSize(
        ViewportState state,
        MmPoint? centerSceneMm = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        MmPoint center = centerSceneMm ??
            ViewportTransform.DipToSceneMm(
                new Point(
                    state.ViewportDip.Width / 2.0,
                    state.ViewportDip.Height / 2.0),
                state);

        var actual = new ViewportState(
            1.0,
            state.PanDip,
            state.ViewportDip,
            state.MinZoom,
            state.MaxZoom);

        return CenterOn(actual, center);
    }

    public ViewportState CenterOn(
        ViewportState state,
        MmPoint point)
    {
        ArgumentNullException.ThrowIfNull(state);

        double scale =
            ViewportTransform.Scale(state);
        var pan = new Vector(
            (state.ViewportDip.Width / 2.0) -
                (point.X * scale),
            (state.ViewportDip.Height / 2.0) -
                (point.Y * scale));

        return new ViewportState(
            state.Zoom,
            pan,
            state.ViewportDip,
            state.MinZoom,
            state.MaxZoom);
    }

    private static ViewportState Fit(
        ViewportState state,
        MmRect bounds,
        double marginDip)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!double.IsFinite(marginDip) || marginDip < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(marginDip));
        }

        double availableWidth =
            state.ViewportDip.Width - (2.0 * marginDip);
        double availableHeight =
            state.ViewportDip.Height - (2.0 * marginDip);

        if (availableWidth <= 0 || availableHeight <= 0)
        {
            throw new InvalidOperationException(
                "Viewport is too small for the requested margin.");
        }

        double scale = Math.Min(
            availableWidth / bounds.Width,
            availableHeight / bounds.Height);
        double zoom = Math.Clamp(
            scale / ViewportTransform.DipsPerMillimetre,
            state.MinZoom,
            state.MaxZoom);
        double actualScale =
            ViewportTransform.DipsPerMillimetre * zoom;
        double centerX =
            bounds.X + (bounds.Width / 2.0);
        double centerY =
            bounds.Y + (bounds.Height / 2.0);
        var pan = new Vector(
            (state.ViewportDip.Width / 2.0) -
                (centerX * actualScale),
            (state.ViewportDip.Height / 2.0) -
                (centerY * actualScale));

        return new ViewportState(
            zoom,
            pan,
            state.ViewportDip,
            state.MinZoom,
            state.MaxZoom);
    }
}
