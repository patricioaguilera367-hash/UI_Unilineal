using Avalonia;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Rendering.Avalonia.Viewport;

public static class ViewportTransform
{
    public const double DipsPerMillimetre = 96.0 / 25.4;

    public static Point SceneMmToDip(
        MmPoint point,
        ViewportState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        double scale = Scale(state);
        return new Point(
            (point.X * scale) + state.PanDip.X,
            (point.Y * scale) + state.PanDip.Y);
    }

    public static MmPoint DipToSceneMm(
        Point point,
        ViewportState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        double scale = Scale(state);
        return new MmPoint(
            (point.X - state.PanDip.X) / scale,
            (point.Y - state.PanDip.Y) / scale);
    }

    public static Rect SceneRectMmToDip(
        MmRect rect,
        ViewportState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        Point topLeft =
            SceneMmToDip(
                new MmPoint(rect.X, rect.Y),
                state);
        double scale = Scale(state);

        return new Rect(
            topLeft.X,
            topLeft.Y,
            rect.Width * scale,
            rect.Height * scale);
    }

    public static MmRect DipRectToSceneMm(
        Rect rect,
        ViewportState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        MmPoint topLeft =
            DipToSceneMm(
                new Point(rect.X, rect.Y),
                state);
        double scale = Scale(state);

        return new MmRect(
            topLeft.X,
            topLeft.Y,
            rect.Width / scale,
            rect.Height / scale);
    }

    internal static double Scale(ViewportState state) =>
        DipsPerMillimetre * state.Zoom;
}
