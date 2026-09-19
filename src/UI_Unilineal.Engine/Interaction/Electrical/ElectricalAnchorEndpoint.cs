using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Electrical;

public sealed record ElectricalAnchorEndpoint
{
    public ElectricalAnchorEndpoint(
        EntityReference entity,
        string anchorId,
        AnchorRole role)
    {
        Entity =
            entity ??
            throw new ArgumentNullException(
                nameof(entity));

        if (string.IsNullOrWhiteSpace(anchorId))
        {
            throw new ArgumentException(
                "Anchor ID is required.",
                nameof(anchorId));
        }

        AnchorId = anchorId;
        Role = role;
    }

    public EntityReference Entity { get; }

    public string AnchorId { get; }

    public AnchorRole Role { get; }
}
