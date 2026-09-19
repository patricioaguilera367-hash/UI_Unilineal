using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Interaction;

public sealed record InteractionEvent(
    InteractionEventKind Kind,
    SceneId? SceneElementId = null,
    string? AnchorId = null);
