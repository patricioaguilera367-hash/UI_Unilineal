using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Rendering.Avalonia.HitTesting;

namespace UI_Unilineal.Rendering.Avalonia.Interaction;

public sealed record InteractionGestureState(
    HitTestResult? PressHit,
    MmPoint PressScenePoint,
    MmPoint CurrentScenePoint,
    MmRect? SourceBounds)
{
    public static InteractionGestureState Empty { get; } =
        new(
            null,
            default,
            default,
            null);
}
