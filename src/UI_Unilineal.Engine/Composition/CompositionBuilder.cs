using System.Globalization;
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

    public DrawingComposition BuildBoardDetail(
        SingleLineProjection projection,
        EntityUid boardUid,
        RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(profile);

        EnsureRequiredBlocks(
            profile,
            "INCOMING_SUPPLY_BLOCK",
            "SERVICE_ENTRANCE_ASSEMBLY_BLOCK",
            "MAIN_PROTECTION_BLOCK",
            "MAIN_BUS_BLOCK",
            "NEUTRAL_BUS_BLOCK",
            "PE_BUS_BLOCK",
            "CIRCUIT_BRANCH_BLOCK",
            "PROTECTION_CHAIN_BLOCK",
            "DOWNSTREAM_BOARD_BLOCK",
            "FINAL_LOAD_BLOCK",
            "GROUNDING_BLOCK",
            "UNKNOWN_BLOCK");

        BoardDetailProjection detail = projection.BoardDetails
            .SingleOrDefault(item => item.Board.Uid == boardUid)
            ?? throw new KeyNotFoundException(
                $"Board detail '{boardUid}' does not exist in the projection.");

        var blocks = new List<CompositionBlock>();
        var connections = new List<CompositionConnection>();

        CompositionBlock[] incomingBlocks = detail.IncomingSupplies
            .OrderBy(item => item.Supply.Priority)
            .ThenBy(item => item.Supply.Role)
            .ThenBy(item => item.Supply.Uid.Value, StringComparer.Ordinal)
            .Select(item => CreateIncomingBlock(boardUid, item))
            .ToArray();
        blocks.AddRange(incomingBlocks);

        CompositionBlock[] mainProtections = detail.MainBus.MainProtections
            .Select(item => CreateMainProtectionBlock(boardUid, item))
            .ToArray();
        blocks.AddRange(mainProtections);

        CompositionBlock bus = CreateBusBlock(boardUid, detail.MainBus);
        blocks.Add(bus);

        string tapList = string.Join(
            "|",
            detail.Branches
                .Select(branch => branch.Circuit.Uid.Value));

        CompositionBlock neutralBus = CreateStructuralRail(
            CompositionIdFactory.DetailNeutralBus(boardUid),
            "NEUTRAL_BUS_BLOCK",
            "NeutralBus",
            boardUid,
            tapList);
        CompositionBlock protectiveEarthBus = CreateStructuralRail(
            CompositionIdFactory.DetailProtectiveEarthBus(boardUid),
            "PE_BUS_BLOCK",
            "ProtectiveEarthBus",
            boardUid,
            tapList);

        blocks.Add(neutralBus);
        blocks.Add(protectiveEarthBus);

        ConnectIncomingPath(
            boardUid,
            incomingBlocks,
            mainProtections,
            bus,
            connections);

        foreach (BranchProjection branch in detail.Branches)
        {
            AddBranch(
                boardUid,
                bus,
                neutralBus,
                protectiveEarthBus,
                branch,
                blocks,
                connections);
        }

        foreach (GroundingInput grounding in detail.Grounding)
        {
            AddGrounding(
                boardUid,
                bus,
                grounding,
                blocks,
                connections);
        }

        return new DrawingComposition(
            DrawingCompositionKind.BoardDetail,
            new EntityReference(boardUid, EntityKind.Board),
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

    private static CompositionBlock CreateIncomingBlock(
        EntityUid boardUid,
        IncomingSupplyProjection incoming)
    {
        string label = incoming.Source is not null
            ? DisplayLabel(incoming.Source.Code, incoming.Source.Name)
            : incoming.OriginBoard is not null
                ? DisplayLabel(incoming.OriginBoard.Code, incoming.OriginBoard.Name)
                : incoming.Supply.Origin.Uid.ToString();

        if (incoming.Source is not null &&
            incoming.ServiceEntrance is ServiceEntranceInput serviceEntrance)
        {
            var labels =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["TITLE"] = "EMPALME",
                    ["METER_CODE"] =
                        serviceEntrance.MeterKind switch
                        {
                            MeterKind.SinglePhase => "M 1F",
                            MeterKind.ThreePhase => "M 3F",
                            MeterKind.Other => "M",
                            _ => "M ?"
                        },
                    ["TARIFF"] =
                        string.IsNullOrWhiteSpace(serviceEntrance.TariffCode)
                            ? "Tarifa: —"
                            : $"Tarifa: {serviceEntrance.TariffCode}",
                    ["DETAILS"] =
                        string.IsNullOrWhiteSpace(serviceEntrance.Details)
                            ? "Detalle: —"
                            : serviceEntrance.Details
                };

            var overrides =
                new Dictionary<string, string>(StringComparer.Ordinal);

            if (incoming.ServiceProtection is ProtectionInput protection)
            {
                (_, IReadOnlyDictionary<string, string> protectionOverrides) =
                    ProtectionPresentation(
                        protection,
                        "PROTECTION");

                foreach ((string key, string value) in protectionOverrides)
                {
                    overrides[key] = value;
                }

                IReadOnlyDictionary<string, string> protectionLabels =
                    ProtectionLabels(protection);

                labels["PROTECTION_TEXT"] =
                    protectionLabels.TryGetValue(
                        "RATING",
                        out string? rating)
                        ? $"{rating} · {serviceEntrance.ProtectionAuthority}"
                        : serviceEntrance.ProtectionAuthority.ToString();
            }
            else
            {
                labels["PROTECTION_TEXT"] =
                    $"Protección: — · {serviceEntrance.ProtectionAuthority}";
            }

            return new CompositionBlock(
                CompositionIdFactory.DetailIncoming(
                    boardUid,
                    incoming.Supply.Uid),
                "SERVICE_ENTRANCE_ASSEMBLY_BLOCK",
                "ServiceEntranceAssembly",
                new EntityReference(
                    incoming.Supply.Uid,
                    EntityKind.SupplyConnection),
                labels,
                incoming.Status,
                CompositionIdFactory.DetailBoard(boardUid),
                overrides);
        }

        return new CompositionBlock(
            CompositionIdFactory.DetailIncoming(
                boardUid,
                incoming.Supply.Uid),
            "INCOMING_SUPPLY_BLOCK",
            "IncomingSupply",
            new EntityReference(
                incoming.Supply.Uid,
                EntityKind.SupplyConnection),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CODE"] = label
            },
            incoming.Status,
            CompositionIdFactory.DetailBoard(boardUid));
    }

    private static CompositionBlock CreateMainProtectionBlock(
        EntityUid boardUid,
        ProtectionInput protection)
    {
        (string definitionId, IReadOnlyDictionary<string, string> overrides) =
            ProtectionPresentation(protection, "BREAKER");

        return new CompositionBlock(
            CompositionIdFactory.DetailMainProtection(
                boardUid,
                protection.Uid),
            definitionId == "PROTECTION_CHAIN_BLOCK"
                ? "MAIN_PROTECTION_BLOCK"
                : definitionId,
            "MainProtection",
            new EntityReference(protection.Uid, EntityKind.Protection),
            ProtectionLabels(protection),
            ProjectionStatusResolver.Resolve(
                protection.DataState,
                null,
                []),
            CompositionIdFactory.DetailBoard(boardUid),
            definitionId == "PROTECTION_CHAIN_BLOCK"
                ? RemapOverride(overrides, "PROTECTION", "BREAKER")
                : overrides);
    }

    private static CompositionBlock CreateBusBlock(
        EntityUid boardUid,
        BusProjection bus) =>
        new(
            CompositionIdFactory.DetailBus(boardUid, bus.Bus.Uid),
            "MAIN_BUS_BLOCK",
            "MainBus",
            new EntityReference(bus.Bus.Uid, EntityKind.Bus),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CODE"] = bus.Bus.Code
            },
            bus.Status,
            CompositionIdFactory.DetailBoard(boardUid));

    private static CompositionBlock CreateStructuralRail(
        string id,
        string blockDefinitionId,
        string semanticRole,
        EntityUid boardUid,
        string tapList) =>
        new(
            id,
            blockDefinitionId,
            semanticRole,
            new EntityReference(boardUid, EntityKind.Board),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TAPS"] = tapList
            },
            ProjectionStatus.Ok,
            CompositionIdFactory.DetailBoard(boardUid));

    private static void ConnectIncomingPath(
        EntityUid boardUid,
        IReadOnlyList<CompositionBlock> incoming,
        IReadOnlyList<CompositionBlock> mainProtections,
        CompositionBlock bus,
        ICollection<CompositionConnection> connections)
    {
        string firstTarget = mainProtections.Count > 0
            ? mainProtections[0].Id
            : bus.Id;

        foreach (CompositionBlock incomingBlock in incoming)
        {
            connections.Add(new CompositionConnection(
                CompositionIdFactory.DetailConnection(
                    boardUid,
                    "incoming",
                    incomingBlock.Id),
                new CompositionAnchorRef(
                    incomingBlock.Id,
                    AnchorRole.PowerOut,
                    "OUT"),
                new CompositionAnchorRef(
                    firstTarget,
                    AnchorRole.PowerIn,
                    "IN"),
                "POWER",
                incomingBlock.Entity));
        }

        for (int index = 0; index < mainProtections.Count; index++)
        {
            CompositionBlock current = mainProtections[index];
            string target = index + 1 < mainProtections.Count
                ? mainProtections[index + 1].Id
                : bus.Id;

            connections.Add(new CompositionConnection(
                CompositionIdFactory.DetailConnection(
                    boardUid,
                    "main-protection",
                    current.Id),
                new CompositionAnchorRef(
                    current.Id,
                    AnchorRole.PowerOut,
                    "OUT"),
                new CompositionAnchorRef(
                    target,
                    AnchorRole.PowerIn,
                    "IN"),
                "POWER",
                current.Entity));
        }
    }

    private static void AddBranch(
        EntityUid boardUid,
        CompositionBlock bus,
        CompositionBlock neutralBus,
        CompositionBlock protectiveEarthBus,
        BranchProjection branch,
        ICollection<CompositionBlock> blocks,
        ICollection<CompositionConnection> connections)
    {
        string branchId = CompositionIdFactory.DetailBranch(
            boardUid,
            branch.Circuit.Uid);

        var branchBlock = new CompositionBlock(
            branchId,
            "CIRCUIT_BRANCH_BLOCK",
            "CircuitBranch",
            new EntityReference(
                branch.Circuit.Uid,
                EntityKind.Circuit),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NAME"] = DisplayLabel(
                    branch.Circuit.Code,
                    branch.Circuit.Name)
            },
            branch.Status,
            bus.Id);
        blocks.Add(branchBlock);

        connections.Add(new CompositionConnection(
            CompositionIdFactory.DetailConnection(
                boardUid,
                "branch",
                branch.Circuit.Uid.ToString()),
            new CompositionAnchorRef(
                bus.Id,
                AnchorRole.BusTap,
                "TAP"),
            new CompositionAnchorRef(
                branchId,
                AnchorRole.PowerIn,
                "IN"),
            "POWER",
            branchBlock.Entity));

        var chain = new List<CompositionBlock>();

        foreach (ProtectionInput protection in branch.ProtectionChain)
        {
            (string definitionId, IReadOnlyDictionary<string, string> overrides) =
                ProtectionPresentation(protection, "PROTECTION");

            var protectionBlock = new CompositionBlock(
                CompositionIdFactory.DetailProtection(
                    boardUid,
                    branch.Circuit.Uid,
                    protection.Uid),
                definitionId,
                protection.Kind == ProtectionKind.Differential
                    ? "DifferentialProtection"
                    : "Protection",
                new EntityReference(
                    protection.Uid,
                    EntityKind.Protection),
                ProtectionLabels(protection),
                ProjectionStatusResolver.Resolve(
                    protection.DataState,
                    null,
                    []),
                branchId,
                overrides);
            blocks.Add(protectionBlock);
            chain.Add(protectionBlock);
        }

        CompositionBlock destination = CreateDestinationBlock(
            boardUid,
            branch);
        blocks.Add(destination);

        string previousId = branchId;
        EntityReference? previousEntity = branchBlock.Entity;

        foreach (CompositionBlock protection in chain)
        {
            connections.Add(new CompositionConnection(
                CompositionIdFactory.DetailConnection(
                    boardUid,
                    "protection",
                    protection.Id),
                new CompositionAnchorRef(
                    previousId,
                    AnchorRole.PowerOut,
                    "OUT"),
                new CompositionAnchorRef(
                    protection.Id,
                    AnchorRole.PowerIn,
                    "IN"),
                "POWER",
                protection.Entity));

            previousId = protection.Id;
            previousEntity = protection.Entity;
        }

        connections.Add(new CompositionConnection(
            CompositionIdFactory.DetailConnection(
                boardUid,
                "destination",
                branch.Circuit.Uid.ToString()),
            new CompositionAnchorRef(
                previousId,
                AnchorRole.PowerOut,
                "OUT"),
            new CompositionAnchorRef(
                destination.Id,
                AnchorRole.PowerIn,
                "IN"),
            "POWER",
            previousEntity));

        ProtectionInput? differential = branch.ProtectionChain
            .FirstOrDefault(protection =>
                protection.Kind == ProtectionKind.Differential);

        string neutralTapId = $"TAP:{branch.Circuit.Uid.Value}";
        string peTapId = $"TAP:{branch.Circuit.Uid.Value}";

        if (differential is not null)
        {
            string rcdId = CompositionIdFactory.DetailProtection(
                boardUid,
                branch.Circuit.Uid,
                differential.Uid);

            connections.Add(new CompositionConnection(
                CompositionIdFactory.DetailConnection(
                    boardUid,
                    "neutral-in",
                    branch.Circuit.Uid.ToString()),
                new CompositionAnchorRef(
                    neutralBus.Id,
                    AnchorRole.Neutral,
                    neutralTapId),
                new CompositionAnchorRef(
                    rcdId,
                    AnchorRole.Neutral,
                    "N_IN"),
                "NEUTRAL_AUX",
                branchBlock.Entity));

            connections.Add(new CompositionConnection(
                CompositionIdFactory.DetailConnection(
                    boardUid,
                    "neutral-out",
                    branch.Circuit.Uid.ToString()),
                new CompositionAnchorRef(
                    rcdId,
                    AnchorRole.Neutral,
                    "N_OUT"),
                new CompositionAnchorRef(
                    destination.Id,
                    AnchorRole.Neutral,
                    "N"),
                "NEUTRAL_AUX",
                branchBlock.Entity));
        }
        else
        {
            connections.Add(new CompositionConnection(
                CompositionIdFactory.DetailConnection(
                    boardUid,
                    "neutral",
                    branch.Circuit.Uid.ToString()),
                new CompositionAnchorRef(
                    neutralBus.Id,
                    AnchorRole.Neutral,
                    neutralTapId),
                new CompositionAnchorRef(
                    destination.Id,
                    AnchorRole.Neutral,
                    "N"),
                "NEUTRAL_AUX",
                branchBlock.Entity));
        }

        connections.Add(new CompositionConnection(
            CompositionIdFactory.DetailConnection(
                boardUid,
                "protective-earth",
                branch.Circuit.Uid.ToString()),
            new CompositionAnchorRef(
                protectiveEarthBus.Id,
                AnchorRole.Ground,
                peTapId),
            new CompositionAnchorRef(
                destination.Id,
                AnchorRole.Ground,
                "PE"),
            "GROUND_AUX",
            branchBlock.Entity));
    }

    private static CompositionBlock CreateDestinationBlock(
        EntityUid boardUid,
        BranchProjection branch)
    {
        string definitionId = branch.Kind switch
        {
            BranchKind.DownstreamBoard => "DOWNSTREAM_BOARD_BLOCK",
            BranchKind.FinalCircuit => "FINAL_LOAD_BLOCK",
            _ => "UNKNOWN_BLOCK"
        };

        EntityReference? entity = branch.Destination.Entity;
        if (entity is null && branch.Kind == BranchKind.FinalCircuit)
        {
            entity = new EntityReference(
                new EntityUid($"LOAD:{branch.Circuit.Uid}"),
                EntityKind.Load);
        }

        return new CompositionBlock(
            CompositionIdFactory.DetailDestination(
                boardUid,
                branch.Circuit.Uid),
            definitionId,
            branch.Kind switch
            {
                BranchKind.DownstreamBoard => "DownstreamBoard",
                BranchKind.FinalCircuit => "FinalLoad",
                _ => "Unknown"
            },
            entity,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["NAME"] = branch.Destination.Label
            },
            branch.Status,
            CompositionIdFactory.DetailBranch(
                boardUid,
                branch.Circuit.Uid));
    }

    private static void AddGrounding(
        EntityUid boardUid,
        CompositionBlock bus,
        GroundingInput grounding,
        ICollection<CompositionBlock> blocks,
        ICollection<CompositionConnection> connections)
    {
        string groundingId = CompositionIdFactory.DetailGrounding(
            boardUid,
            grounding.Uid);

        var groundingBlock = new CompositionBlock(
            groundingId,
            "GROUNDING_BLOCK",
            "Grounding",
            new EntityReference(
                grounding.Uid,
                EntityKind.Grounding),
            new Dictionary<string, string>(StringComparer.Ordinal),
            ProjectionStatusResolver.Resolve(
                grounding.DataState,
                null,
                []),
            bus.Id);
        blocks.Add(groundingBlock);

        connections.Add(new CompositionConnection(
            CompositionIdFactory.DetailConnection(
                boardUid,
                "grounding",
                grounding.Uid.ToString()),
            new CompositionAnchorRef(
                bus.Id,
                AnchorRole.Ground,
                "GROUND"),
            new CompositionAnchorRef(
                groundingId,
                AnchorRole.Ground,
                "GROUND"),
            "GROUND",
            groundingBlock.Entity));
    }

    private static (
        string DefinitionId,
        IReadOnlyDictionary<string, string> Overrides)
        ProtectionPresentation(
            ProtectionInput protection,
            string partId)
    {
        string? symbolId = protection.Kind switch
        {
            ProtectionKind.Breaker when protection.Poles is >= 1 and <= 4 =>
                $"BREAKER_{protection.Poles}X",
            ProtectionKind.Breaker => "BREAKER",
            ProtectionKind.Differential when protection.Poles == 2 =>
                "RCD_2X",
            ProtectionKind.Differential when protection.Poles == 4 =>
                "RCD_4X",
            ProtectionKind.Differential => "RCD",
            ProtectionKind.Fuse => "FUSE",
            _ => null
        };

        if (symbolId is null)
        {
            return (
                "UNKNOWN_BLOCK",
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        return (
            "PROTECTION_CHAIN_BLOCK",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [partId] = symbolId
            });
    }

    private static IReadOnlyDictionary<string, string> RemapOverride(
        IReadOnlyDictionary<string, string> source,
        string from,
        string to)
    {
        if (!source.TryGetValue(from, out string? value))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [to] = value
        };
    }

    private static IReadOnlyDictionary<string, string> ProtectionLabels(
        ProtectionInput protection)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        string poles =
            protection.Poles is int poleCount
                ? $"{poleCount}x"
                : string.Empty;

        if (protection.Kind == ProtectionKind.Differential)
        {
            var lines = new List<string>();

            if (protection.RatedCurrentA is decimal rated)
            {
                lines.Add(
                    $"{poles}{rated.ToString("0.##", CultureInfo.InvariantCulture)} A");
            }

            if (protection.DifferentialCurrentMa is decimal differential)
            {
                lines.Add(
                    $"{differential.ToString("0.##", CultureInfo.InvariantCulture)} mA");
            }

            if (!string.IsNullOrWhiteSpace(protection.DifferentialType))
            {
                lines.Add($"Tipo {protection.DifferentialType}");
            }

            if (lines.Count > 0)
            {
                labels["RATING"] = string.Join(" / ", lines);
            }

            return labels;
        }

        if (protection.RatedCurrentA is decimal ratedCurrent)
        {
            var lines = new List<string>
            {
                $"{poles}{ratedCurrent.ToString("0.##", CultureInfo.InvariantCulture)} A"
            };

            if (protection.BreakingCapacityKa is decimal breaking)
            {
                lines.Add(
                    $"{breaking.ToString("0.##", CultureInfo.InvariantCulture)} kA");
            }

            if (!string.IsNullOrWhiteSpace(protection.Curve))
            {
                lines.Add($"Curva {protection.Curve}");
            }

            labels["RATING"] = string.Join(" / ", lines);
        }

        return labels;
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
