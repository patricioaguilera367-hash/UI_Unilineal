using System.Collections.ObjectModel;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Composition;

public enum DrawingCompositionKind
{
    Summary,
    BoardDetail
}

public sealed class CompositionBlock
{
    public CompositionBlock(
        string id,
        string blockDefinitionId,
        string semanticRole,
        EntityReference? entity,
        IReadOnlyDictionary<string, string> labels,
        ProjectionStatus status,
        string? parentId,
        IReadOnlyDictionary<string, string>? symbolOverrides = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Composition block ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(blockDefinitionId))
        {
            throw new ArgumentException("Block definition ID is required.", nameof(blockDefinitionId));
        }

        if (string.IsNullOrWhiteSpace(semanticRole))
        {
            throw new ArgumentException("Semantic role is required.", nameof(semanticRole));
        }

        ArgumentNullException.ThrowIfNull(labels);

        Id = id;
        BlockDefinitionId = blockDefinitionId;
        SemanticRole = semanticRole;
        Entity = entity;
        Labels = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(labels, StringComparer.Ordinal));
        Status = status;
        ParentId = parentId;
        SymbolOverrides = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(
                symbolOverrides ?? new Dictionary<string, string>(),
                StringComparer.Ordinal));
    }

    public string Id { get; }

    public string BlockDefinitionId { get; }

    public string SemanticRole { get; }

    public EntityReference? Entity { get; }

    public IReadOnlyDictionary<string, string> Labels { get; }

    public ProjectionStatus Status { get; }

    public string? ParentId { get; }

    public IReadOnlyDictionary<string, string> SymbolOverrides { get; }
}

public sealed record CompositionAnchorRef
{
    public CompositionAnchorRef(
        string blockId,
        AnchorRole role,
        string? preferredAnchorId)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException("Block ID is required.", nameof(blockId));
        }

        if (preferredAnchorId is not null &&
            string.IsNullOrWhiteSpace(preferredAnchorId))
        {
            throw new ArgumentException(
                "Preferred anchor ID cannot be blank.",
                nameof(preferredAnchorId));
        }

        BlockId = blockId;
        Role = role;
        PreferredAnchorId = preferredAnchorId;
    }

    public string BlockId { get; }

    public AnchorRole Role { get; }

    public string? PreferredAnchorId { get; }
}

public sealed record CompositionConnection
{
    public CompositionConnection(
        string id,
        CompositionAnchorRef source,
        CompositionAnchorRef target,
        string lineStyleId,
        EntityReference? semanticEntity)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Connection ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(lineStyleId))
        {
            throw new ArgumentException("Line style ID is required.", nameof(lineStyleId));
        }

        Id = id;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        LineStyleId = lineStyleId;
        SemanticEntity = semanticEntity;
    }

    public string Id { get; }

    public CompositionAnchorRef Source { get; }

    public CompositionAnchorRef Target { get; }

    public string LineStyleId { get; }

    public EntityReference? SemanticEntity { get; }
}

public sealed class DrawingComposition
{
    public DrawingComposition(
        DrawingCompositionKind kind,
        EntityReference? scope,
        IEnumerable<CompositionBlock> blocks,
        IEnumerable<CompositionConnection> connections,
        string inputFingerprint,
        string profileFingerprint)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(connections);

        if (string.IsNullOrWhiteSpace(inputFingerprint))
        {
            throw new ArgumentException("Input fingerprint is required.", nameof(inputFingerprint));
        }

        if (string.IsNullOrWhiteSpace(profileFingerprint))
        {
            throw new ArgumentException("Profile fingerprint is required.", nameof(profileFingerprint));
        }

        Kind = kind;
        Scope = scope;
        Blocks = Array.AsReadOnly(blocks.ToArray());
        Connections = Array.AsReadOnly(connections.ToArray());
        InputFingerprint = inputFingerprint;
        ProfileFingerprint = profileFingerprint;
    }

    public DrawingCompositionKind Kind { get; }

    public EntityReference? Scope { get; }

    public IReadOnlyList<CompositionBlock> Blocks { get; }

    public IReadOnlyList<CompositionConnection> Connections { get; }

    public string InputFingerprint { get; }

    public string ProfileFingerprint { get; }
}
