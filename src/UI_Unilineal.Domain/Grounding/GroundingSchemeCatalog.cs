namespace UI_Unilineal.Domain.Grounding;

public sealed record GroundingSchemeCatalog
{
    public GroundingSchemeCatalog(
        string selectionMode,
        string defaultSchemeId,
        IReadOnlyList<GroundingSchemeDefinition> schemes,
        IReadOnlyList<GroundingPreviewPresetDefinition> previewPresets)
    {
        if (string.IsNullOrWhiteSpace(selectionMode))
        {
            throw new ArgumentException(
                "Grounding scheme selection mode is required.",
                nameof(selectionMode));
        }

        if (string.IsNullOrWhiteSpace(defaultSchemeId))
        {
            throw new ArgumentException(
                "Default grounding scheme ID is required.",
                nameof(defaultSchemeId));
        }

        ArgumentNullException.ThrowIfNull(schemes);

        if (schemes.Count == 0)
        {
            throw new ArgumentException(
                "At least one grounding scheme is required.",
                nameof(schemes));
        }

        if (!schemes.Any(scheme =>
                string.Equals(
                    scheme.Id,
                    defaultSchemeId,
                    StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                $"Default grounding scheme '{defaultSchemeId}' does not exist.",
                nameof(defaultSchemeId));
        }

        ArgumentNullException.ThrowIfNull(previewPresets);

        SelectionMode = selectionMode;
        DefaultSchemeId = defaultSchemeId;
        Schemes = Array.AsReadOnly(schemes.ToArray());
        PreviewPresets =
            Array.AsReadOnly(previewPresets.ToArray());

        foreach (GroundingSchemeDefinition scheme in Schemes)
        {
            if (scheme.PreviewPresetId is null)
            {
                continue;
            }

            if (!PreviewPresets.Any(preset =>
                    string.Equals(
                        preset.Id,
                        scheme.PreviewPresetId,
                        StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    $"Grounding scheme '{scheme.Id}' references missing preview preset '{scheme.PreviewPresetId}'.",
                    nameof(previewPresets));
            }
        }
    }

    public string SelectionMode { get; }

    public string DefaultSchemeId { get; }

    public IReadOnlyList<GroundingSchemeDefinition> Schemes { get; }

    public IReadOnlyList<GroundingPreviewPresetDefinition> PreviewPresets { get; }
}

public sealed record GroundingPreviewPresetDefinition
{
    public GroundingPreviewPresetDefinition(
        string id,
        string status,
        IReadOnlyList<GroundingPreviewSymbolPlacement> symbols,
        IReadOnlyList<GroundingPreviewSegment> segments)
    {
        Id = string.IsNullOrWhiteSpace(id)
            ? throw new ArgumentException("Required.", nameof(id))
            : id;
        Status = string.IsNullOrWhiteSpace(status)
            ? throw new ArgumentException("Required.", nameof(status))
            : status;
        Symbols = Array.AsReadOnly(
            (symbols ?? throw new ArgumentNullException(nameof(symbols)))
                .ToArray());
        Segments = Array.AsReadOnly(
            (segments ?? throw new ArgumentNullException(nameof(segments)))
                .ToArray());
    }

    public string Id { get; }

    public string Status { get; }

    public IReadOnlyList<GroundingPreviewSymbolPlacement> Symbols { get; }

    public IReadOnlyList<GroundingPreviewSegment> Segments { get; }
}

public sealed record GroundingPreviewSymbolPlacement(
    string SymbolId,
    double X,
    double Y);

public sealed record GroundingPreviewSegment(
    double X1,
    double Y1,
    double X2,
    double Y2);

public sealed record GroundingSchemeDefinition
{
    public GroundingSchemeDefinition(
        string id,
        string displayName,
        string sourceDocument,
        string sourceSection,
        string? sourceFigure,
        bool defaultSelected,
        IReadOnlyList<string> symbolIds,
        string? previewPresetId,
        string connectionStatus,
        IReadOnlyList<GroundingConnectionPointDefinition> connectionPoints)
    {
        Id = Required(id, nameof(id));
        DisplayName = Required(displayName, nameof(displayName));
        SourceDocument = Required(sourceDocument, nameof(sourceDocument));
        SourceSection = Required(sourceSection, nameof(sourceSection));
        SourceFigure = sourceFigure;
        DefaultSelected = defaultSelected;
        SymbolIds = Array.AsReadOnly(
            (symbolIds ?? throw new ArgumentNullException(nameof(symbolIds)))
                .ToArray());
        PreviewPresetId = previewPresetId;
        ConnectionStatus = Required(
            connectionStatus,
            nameof(connectionStatus));
        ConnectionPoints = Array.AsReadOnly(
            (connectionPoints ??
             throw new ArgumentNullException(nameof(connectionPoints)))
                .ToArray());
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string SourceDocument { get; }

    public string SourceSection { get; }

    public string? SourceFigure { get; }

    public bool DefaultSelected { get; }

    public IReadOnlyList<string> SymbolIds { get; }

    public string? PreviewPresetId { get; }

    public string ConnectionStatus { get; }

    public IReadOnlyList<GroundingConnectionPointDefinition> ConnectionPoints { get; }

    private static string Required(
        string value,
        string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}

public sealed record GroundingConnectionPointDefinition(
    string Id,
    string SymbolId,
    string TargetRole);
