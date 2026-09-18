using Avalonia;

namespace UI_Unilineal.Rendering.Avalonia.Viewport;

public sealed record ViewportState
{
    public ViewportState(
        double zoom,
        Vector panDip,
        Size viewportDip,
        double minZoom = 0.05,
        double maxZoom = 32.0)
    {
        if (!double.IsFinite(minZoom) || minZoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minZoom));
        }

        if (!double.IsFinite(maxZoom) || maxZoom < minZoom)
        {
            throw new ArgumentOutOfRangeException(nameof(maxZoom));
        }

        if (!double.IsFinite(zoom) || zoom < minZoom || zoom > maxZoom)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }

        if (!double.IsFinite(panDip.X) || !double.IsFinite(panDip.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(panDip));
        }

        if (!double.IsFinite(viewportDip.Width) ||
            !double.IsFinite(viewportDip.Height) ||
            viewportDip.Width < 0 ||
            viewportDip.Height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportDip));
        }

        Zoom = zoom;
        PanDip = panDip;
        ViewportDip = viewportDip;
        MinZoom = minZoom;
        MaxZoom = maxZoom;
    }

    public double Zoom { get; }

    public Vector PanDip { get; }

    public Size ViewportDip { get; }

    public double MinZoom { get; }

    public double MaxZoom { get; }
}
