namespace UI_Unilineal.Domain.Scene;

public readonly record struct SceneId
{
    public SceneId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Scene ID is required.", nameof(value));
        }

        if (value.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Scene ID cannot contain whitespace.", nameof(value));
        }

        if (!value.Contains('/', StringComparison.Ordinal) ||
            value.StartsWith("/", StringComparison.Ordinal) ||
            value.EndsWith("/", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Scene ID must be a deterministic hierarchical path.",
                nameof(value));
        }

        if (Guid.TryParse(value, out _))
        {
            throw new ArgumentException(
                "Random GUID values are not valid Scene IDs.",
                nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
