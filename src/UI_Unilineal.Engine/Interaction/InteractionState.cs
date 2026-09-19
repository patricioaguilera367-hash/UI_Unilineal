using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Interaction;

public sealed record InteractionState(
    InteractionMode Mode,
    InteractionStateKind Kind,
    SceneId? ActiveSceneElementId = null,
    string? ActiveAnchorId = null);
