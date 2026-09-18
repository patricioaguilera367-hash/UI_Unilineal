using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Domain.Blocks;

public sealed record BlockPartDefinition
{
    public BlockPartDefinition(
        string id,
        string symbolId,
        MmPoint offset,
        string? labelSlotId)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Block part ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(symbolId))
        {
            throw new ArgumentException("Symbol ID is required.", nameof(symbolId));
        }

        if (labelSlotId is not null && string.IsNullOrWhiteSpace(labelSlotId))
        {
            throw new ArgumentException("Label slot ID cannot be blank.", nameof(labelSlotId));
        }

        Id = id;
        SymbolId = symbolId;
        Offset = offset;
        LabelSlotId = labelSlotId;
    }

    public string Id { get; }

    public string SymbolId { get; }

    public MmPoint Offset { get; }

    public string? LabelSlotId { get; }
}

public sealed class BlockDefinition
{
    public BlockDefinition(
        string id,
        string semanticRole,
        MmSize minimumSize,
        IEnumerable<BlockPartDefinition> parts,
        IEnumerable<string> provenanceIds)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Block ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(semanticRole))
        {
            throw new ArgumentException("Semantic role is required.", nameof(semanticRole));
        }

        ArgumentNullException.ThrowIfNull(parts);
        ArgumentNullException.ThrowIfNull(provenanceIds);

        BlockPartDefinition[] copiedParts = parts.ToArray();
        string? duplicatePartId = copiedParts
            .GroupBy(part => part.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicatePartId is not null)
        {
            throw new ArgumentException(
                $"Duplicate block part ID '{duplicatePartId}'.",
                nameof(parts));
        }

        string[] copiedProvenance = provenanceIds.ToArray();
        if (copiedProvenance.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Provenance IDs cannot contain blanks.",
                nameof(provenanceIds));
        }

        string? duplicateProvenance = copiedProvenance
            .GroupBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateProvenance is not null)
        {
            throw new ArgumentException(
                $"Duplicate provenance ID '{duplicateProvenance}'.",
                nameof(provenanceIds));
        }

        Id = id;
        SemanticRole = semanticRole;
        MinimumSize = minimumSize;
        Parts = Array.AsReadOnly(copiedParts);
        ProvenanceIds = Array.AsReadOnly(copiedProvenance);
    }

    public string Id { get; }

    public string SemanticRole { get; }

    public MmSize MinimumSize { get; }

    public IReadOnlyList<BlockPartDefinition> Parts { get; }

    public IReadOnlyList<string> ProvenanceIds { get; }
}
