namespace UI_Unilineal.Domain.Semantics;

public sealed record EntityUid
{
    public EntityUid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Entity UID cannot be blank.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
