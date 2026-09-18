namespace UI_Unilineal.Domain.Semantics;

public sealed record EntityReference
{
    public EntityReference(EntityUid uid, EntityKind kind)
    {
        Uid = uid ?? throw new ArgumentNullException(nameof(uid));
        Kind = kind;
    }

    public EntityUid Uid { get; }

    public EntityKind Kind { get; }
}
