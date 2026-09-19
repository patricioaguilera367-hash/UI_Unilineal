using System.Collections.ObjectModel;
using UI_Unilineal.Domain.Profiles;

namespace UI_Unilineal.Engine.Documents;

public sealed record ResolvedLineStyle(
    string Id,
    LineSemanticRole Role,
    double WidthMm,
    LinePattern Pattern);

public sealed record ResolvedTextStyle(
    string Id,
    string FontFamily,
    double HeightMm,
    bool Bold);

public sealed class ResolvedDrawingStyleSet
{
    private readonly IReadOnlyDictionary<string, ResolvedLineStyle> lineStyles;
    private readonly IReadOnlyDictionary<string, ResolvedTextStyle> textStyles;

    public ResolvedDrawingStyleSet(
        string profileId,
        string profileVersion,
        string profileFingerprint,
        IEnumerable<ResolvedLineStyle> lineStyles,
        IEnumerable<ResolvedTextStyle> textStyles)
    {
        ProfileId = Required(profileId, nameof(profileId));
        ProfileVersion = Required(profileVersion, nameof(profileVersion));
        ProfileFingerprint = Required(profileFingerprint, nameof(profileFingerprint));
        ArgumentNullException.ThrowIfNull(lineStyles);
        ArgumentNullException.ThrowIfNull(textStyles);

        Dictionary<string, ResolvedLineStyle> lineMap = lineStyles
            .ToDictionary(style => style.Id, StringComparer.Ordinal);
        Dictionary<string, ResolvedTextStyle> textMap = textStyles
            .ToDictionary(style => style.Id, StringComparer.Ordinal);

        this.lineStyles = new ReadOnlyDictionary<string, ResolvedLineStyle>(lineMap);
        this.textStyles = new ReadOnlyDictionary<string, ResolvedTextStyle>(textMap);
        RequiredFontFamilies = Array.AsReadOnly(
            textMap.Values
                .Select(style => style.FontFamily)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public string ProfileId { get; }

    public string ProfileVersion { get; }

    public string ProfileFingerprint { get; }

    public IReadOnlyList<string> RequiredFontFamilies { get; }

    public ResolvedLineStyle ResolveLine(string id) =>
        lineStyles.TryGetValue(id, out ResolvedLineStyle? style)
            ? style
            : throw new KeyNotFoundException($"Unknown line style '{id}'.");

    public ResolvedTextStyle ResolveText(string id) =>
        textStyles.TryGetValue(id, out ResolvedTextStyle? style)
            ? style
            : throw new KeyNotFoundException($"Unknown text style '{id}'.");

    public bool TryResolveLine(string id, out ResolvedLineStyle? style) =>
        lineStyles.TryGetValue(id, out style);

    public bool TryResolveText(string id, out ResolvedTextStyle? style) =>
        textStyles.TryGetValue(id, out style);

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}
