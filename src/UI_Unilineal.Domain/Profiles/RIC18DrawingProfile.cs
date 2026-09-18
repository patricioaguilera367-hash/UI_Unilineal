using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Domain.Profiles;

public sealed class RIC18DrawingProfile
{
    public RIC18DrawingProfile(
        string profileId,
        string version,
        string? sourceDocument,
        IEnumerable<GraphicRuleSource> provenance,
        IEnumerable<SymbolDefinition> symbols,
        IEnumerable<BlockDefinition> blocks,
        IEnumerable<LineStyleDefinition> lineStyles,
        IEnumerable<TextStyleDefinition> textStyles,
        LayoutProfile layout)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new ArgumentException("Profile ID is required.", nameof(profileId));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Profile version is required.", nameof(version));
        }

        ProfileId = profileId;
        Version = version;
        SourceDocument = sourceDocument;
        Provenance = Copy(provenance, nameof(provenance));
        Symbols = Copy(symbols, nameof(symbols));
        Blocks = Copy(blocks, nameof(blocks));
        LineStyles = Copy(lineStyles, nameof(lineStyles));
        TextStyles = Copy(textStyles, nameof(textStyles));
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    public string ProfileId { get; }

    public string Version { get; }

    public string? SourceDocument { get; }

    public IReadOnlyList<GraphicRuleSource> Provenance { get; }

    public IReadOnlyList<SymbolDefinition> Symbols { get; }

    public IReadOnlyList<BlockDefinition> Blocks { get; }

    public IReadOnlyList<LineStyleDefinition> LineStyles { get; }

    public IReadOnlyList<TextStyleDefinition> TextStyles { get; }

    public LayoutProfile Layout { get; }

    private static IReadOnlyList<T> Copy<T>(
        IEnumerable<T> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }
}
