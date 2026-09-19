using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed record InteractionOverlayState
{
    public InteractionOverlayState(
        SceneId? hovered,
        IReadOnlySet<SceneId> selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        Hovered = hovered;
        Selected = new HashSet<SceneId>(selected);
    }

    public SceneId? Hovered { get; }

    public IReadOnlySet<SceneId> Selected { get; }

    public static InteractionOverlayState Empty { get; } =
        new(
            null,
            new HashSet<SceneId>());
}
