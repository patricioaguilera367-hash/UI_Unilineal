using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Scene;

public sealed record SceneConnection
{
    public SceneConnection(
        SceneId id,
        SceneAnchorRef source,
        SceneAnchorRef target,
        string lineStyleId,
        SceneLayer layer,
        int zIndex,
        SceneVisibility visibility,
        EntityReference? semanticReference)
    {
        if (string.IsNullOrWhiteSpace(lineStyleId))
        {
            throw new ArgumentException("Line style ID is required.", nameof(lineStyleId));
        }

        Id = id;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        LineStyleId = lineStyleId;
        Layer = layer;
        ZIndex = zIndex;
        Visibility = visibility;
        SemanticReference = semanticReference;
    }

    public SceneId Id { get; }

    public SceneAnchorRef Source { get; }

    public SceneAnchorRef Target { get; }

    public string LineStyleId { get; }

    public SceneLayer Layer { get; }

    public int ZIndex { get; }

    public SceneVisibility Visibility { get; }

    public EntityReference? SemanticReference { get; }
}
