using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Composition;

public sealed class CompositionBuilder
{
    public DrawingComposition BuildSummary(
        SingleLineProjection projection,
        RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(profile);

        EnsureRequiredBlocks(
            profile,
            "SOURCE_BLOCK",
            "BOARD_SUMMARY_BLOCK",
            "UNKNOWN_BLOCK");

        var blocksByEntity = new Dictionary<EntityReference, CompositionBlock>();

        foreach (SummaryNode node in projection.Summary.Nodes)
        {
            blocksByEntity[node.Entity] = CreateSummaryNodeBlock(node);
        }

        foreach (SummaryConnection connection in projection.Summary.Connections)
        {
            EnsureSummaryEndpoint(
                blocksByEntity,
                connection.Origin,
                connection.Status);
            EnsureSummaryEndpoint(
                blocksByEntity,
                connection.Destination,
                connection.Status);
        }

        CompositionBlock[] blocks = blocksByEntity.Values
            .OrderBy(block => block.Id, StringComparer.Ordinal)
            .ToArray();

        CompositionConnection[] connections = projection.Summary.Connections
            .OrderBy(
                connection => connection.SupplyConnectionUid.Value,
                StringComparer.Ordinal)
            .Select(CreateSummaryConnection)
            .ToArray();

        return new DrawingComposition(
            DrawingCompositionKind.Summary,
            new EntityReference(projection.ProjectUid, EntityKind.Project),
            blocks,
            connections,
            projection.InputFingerprint,
            DrawingProfileFingerprint.Compute(profile));
    }

    private static CompositionBlock CreateSummaryNodeBlock(SummaryNode node)
    {
        string definitionId = node.Entity.Kind switch
        {
            EntityKind.Source => "SOURCE_BLOCK",
            EntityKind.Board => "BOARD_SUMMARY_BLOCK",
            _ => "UNKNOWN_BLOCK"
        };

        string semanticRole = node.Entity.Kind switch
        {
            EntityKind.Source => "Source",
            EntityKind.Board => "BoardSummary",
            _ => "Unknown"
        };

        return new CompositionBlock(
            CompositionIdFactory.SummaryEntity(node.Entity),
            definitionId,
            semanticRole,
            node.Entity,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NAME"] = DisplayLabel(node.Code, node.Name)
            },
            node.Status,
            null);
    }

    private static void EnsureSummaryEndpoint(
        IDictionary<EntityReference, CompositionBlock> blocks,
        EntityReference entity,
        ProjectionStatus status)
    {
        if (blocks.ContainsKey(entity))
        {
            return;
        }

        blocks.Add(
            entity,
            new CompositionBlock(
                CompositionIdFactory.SummaryEntity(entity),
                "UNKNOWN_BLOCK",
                "Unknown",
                entity,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["NAME"] = entity.Uid.ToString()
                },
                status,
                null));
    }

    private static CompositionConnection CreateSummaryConnection(
        SummaryConnection connection)
    {
        string lineStyleId = connection.Role == SupplyRole.Normal
            ? "POWER"
            : "ALTERNATE_SUPPLY";

        return new CompositionConnection(
            CompositionIdFactory.SummarySupply(connection.SupplyConnectionUid),
            new CompositionAnchorRef(
                CompositionIdFactory.SummaryEntity(connection.Origin),
                AnchorRole.PowerOut,
                "OUT"),
            new CompositionAnchorRef(
                CompositionIdFactory.SummaryEntity(connection.Destination),
                AnchorRole.PowerIn,
                "IN"),
            lineStyleId,
            new EntityReference(
                connection.SupplyConnectionUid,
                EntityKind.SupplyConnection));
    }

    private static string DisplayLabel(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return name;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return code;
        }

        return $"{code} — {name}";
    }

    private static void EnsureRequiredBlocks(
        RIC18DrawingProfile profile,
        params string[] ids)
    {
        HashSet<string> available = profile.Blocks
            .Select(block => block.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string id in ids)
        {
            if (!available.Contains(id))
            {
                throw new InvalidOperationException(
                    $"Drawing profile is missing required block '{id}'.");
            }
        }
    }
}
