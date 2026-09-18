using UI_Unilineal.Domain.Connections;

namespace UI_Unilineal.Domain.Scene;

public sealed record SceneAnchor
{
    public SceneAnchor(
        string id,
        AnchorRole role,
        MmPoint point,
        AnchorDirection direction)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Anchor ID is required.", nameof(id));
        }

        Id = id;
        Role = role;
        Point = point;
        Direction = direction;
    }

    public string Id { get; }

    public AnchorRole Role { get; }

    public MmPoint Point { get; }

    public AnchorDirection Direction { get; }
}

public sealed record SceneAnchorRef
{
    public SceneAnchorRef(SceneId elementId, string anchorId)
    {
        if (string.IsNullOrWhiteSpace(anchorId))
        {
            throw new ArgumentException("Anchor ID is required.", nameof(anchorId));
        }

        ElementId = elementId;
        AnchorId = anchorId;
    }

    public SceneId ElementId { get; }

    public string AnchorId { get; }
}
