using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed record InteractionConnectionPreview(
    MmPoint Start,
    MmPoint End);

public sealed record InteractionOverlayState
{
    public InteractionOverlayState(
        SceneId? hovered,
        IReadOnlySet<SceneId> selected,
        MmRect? layoutGhostBounds = null,
        MmRect? marqueeBounds = null,
        InteractionConnectionPreview? electricalPreview = null)
    {
        ArgumentNullException.ThrowIfNull(selected);

        Hovered = hovered;
        Selected = new HashSet<SceneId>(selected);
        LayoutGhostBounds = layoutGhostBounds;
        MarqueeBounds = marqueeBounds;
        ElectricalPreview = electricalPreview;
    }

    public SceneId? Hovered { get; }

    public IReadOnlySet<SceneId> Selected { get; }

    public MmRect? LayoutGhostBounds { get; }

    public MmRect? MarqueeBounds { get; }

    public InteractionConnectionPreview? ElectricalPreview { get; }

    public static InteractionOverlayState Empty { get; } =
        new(
            null,
            new HashSet<SceneId>());
}
