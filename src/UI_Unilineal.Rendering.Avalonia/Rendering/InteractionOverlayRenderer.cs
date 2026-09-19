using Avalonia;
using Avalonia.Media;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed class InteractionOverlayRenderer
{
    private const double SelectedThicknessDip = 2.0;
    private const double HoverThicknessDip = 1.25;
    private const double SelectedPaddingDip = 3.0;
    private const double HoverPaddingDip = 2.0;

    public void Render(
        DrawingContext context,
        DiagramScene scene,
        ViewportState viewport,
        InteractionOverlayState overlay,
        AvaloniaRenderResources resources)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(resources);

        IReadOnlyDictionary<SceneId, SceneElement> elements =
            scene.Elements.ToDictionary(
                element => element.Id);

        var selectedPen = new Pen(
            resources.ResolveStatusBrush("PENDING"),
            SelectedThicknessDip);
        var hoverPen = new Pen(
            resources.ResolveStatusBrush("WARNING"),
            HoverThicknessDip,
            new DashStyle([4.0, 2.0], 0),
            PenLineCap.Square,
            PenLineJoin.Miter,
            10);

        foreach (SceneId id in overlay.Selected
                     .OrderBy(
                         value => value.Value,
                         StringComparer.Ordinal))
        {
            if (!elements.TryGetValue(
                    id,
                    out SceneElement? element))
            {
                continue;
            }

            context.DrawRectangle(
                selectedPen,
                Inflate(
                    ViewportTransform.SceneRectMmToDip(
                        element.Bounds,
                        viewport),
                    SelectedPaddingDip),
                0);
        }

        if (overlay.Hovered is SceneId hovered &&
            !overlay.Selected.Contains(hovered) &&
            elements.TryGetValue(
                hovered,
                out SceneElement? hoveredElement))
        {
            context.DrawRectangle(
                hoverPen,
                Inflate(
                    ViewportTransform.SceneRectMmToDip(
                        hoveredElement.Bounds,
                        viewport),
                    HoverPaddingDip),
                0);
        }
    }

    private static Rect Inflate(
        Rect rect,
        double amount) =>
        new(
            rect.X - amount,
            rect.Y - amount,
            rect.Width + (amount * 2.0),
            rect.Height + (amount * 2.0));
}
