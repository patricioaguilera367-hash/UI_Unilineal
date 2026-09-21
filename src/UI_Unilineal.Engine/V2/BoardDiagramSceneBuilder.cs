using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Domain.V2;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.V2;

/// <summary>
/// Deterministic RIC-oriented board grammar. Known electrical paths are emitted
/// directly; no generic router is used to discover board topology.
/// </summary>
public sealed class BoardDiagramSceneBuilder
{
    private const string LayoutVersion = "V2-BOARD-GRAMMAR-1";
    private const double MinimumPrimitiveExtentMm = 0.001;
    private const double MarginMm = 10;
    private const double MinimumBoardWidthMm = 80;
    private const double MainBusYmm = 42;
    private const double MainBusExtensionMm = 8;
    private const double IncomingTopYmm = 18;
    private const double IncomingPitchMm = 18;
    private const double BranchBaseHalfWidthMm = 18;
    private const double BranchGapMm = 10;
    private const double TargetPitchMm = 34;
    private const double TargetHalfWidthMm = 13;
    private const double ProtectionStartGapMm = 18;
    private const double ProtectionGapMm = 7;
    private const double JunctionGapMm = 18;
    private const double TargetGapMm = 18;
    private const double BottomMarginMm = 12;
    private const double NodeRadiusMm = 1.2;

    public DiagramScene Build(
        BoardDiagramModel model,
        RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(profile);

        SymbolCatalog symbols = new(profile);
        string fingerprint = Fingerprint(model);
        var elements = new List<SceneElement>();
        var issues = new List<SceneIssue>();

        BoardDiagramIncomingModel[] incomingSupplies =
            model.IncomingSupplies
                .OrderBy(item => item.Role, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.OriginBoardCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SupplyUid.Value, StringComparer.Ordinal)
                .ToArray();

        BranchGeometry[] branches = PlanBranches(model.Branches);
        double contentWidth = branches.Length == 0
            ? 0
            : branches[^1].AxisX + branches[^1].HalfWidth;
        double incomingSpan = Math.Max(
            0,
            (incomingSupplies.Length - 1) * IncomingPitchMm);
        double boardWidth = Math.Max(
            MinimumBoardWidthMm,
            Math.Max(
                contentWidth + (MarginMm * 2),
                incomingSpan + 40));

        if (branches.Length > 0)
        {
            double plannedWidth =
                branches[^1].AxisX +
                branches[^1].HalfWidth;
            double shift =
                (boardWidth - plannedWidth) / 2.0;
            branches = branches
                .Select(item => item with { AxisX = item.AxisX + shift })
                .ToArray();
        }

        double centerX = boardWidth / 2.0;
        double[] incomingXs = CenteredAxes(
            incomingSupplies.Length,
            centerX,
            IncomingPitchMm);

        double busLeft = MarginMm;
        double busRight = boardWidth - MarginMm;
        IEnumerable<double> attachmentXs = incomingXs
            .Concat(branches.Select(item => item.AxisX));

        if (attachmentXs.Any())
        {
            busLeft = Math.Max(
                MarginMm,
                attachmentXs.Min() - MainBusExtensionMm);
            busRight = Math.Min(
                boardWidth - MarginMm,
                attachmentXs.Max() + MainBusExtensionMm);
        }

        AddBoardTitle(
            model,
            boardWidth,
            elements);

        EntityReference boardReference =
            new(model.BoardUid, EntityKind.Board);

        AddMainBus(
            model,
            boardReference,
            busLeft,
            busRight,
            incomingXs,
            branches,
            elements);

        AddIncomingSupplies(
            model,
            incomingSupplies,
            incomingXs,
            boardReference,
            elements);

        AddMainProtection(
            model,
            centerX,
            symbols,
            elements);

        double maximumProtectionBottom =
            branches.Length == 0
                ? MainBusYmm + ProtectionStartGapMm
                : branches.Max(item =>
                    MainBusYmm +
                    ProtectionStartGapMm +
                    ProtectionStackHeight(item.Branch));

        double junctionY =
            maximumProtectionBottom +
            JunctionGapMm;
        double targetTopY =
            junctionY +
            TargetGapMm;

        foreach (BranchGeometry branch in branches)
        {
            AddBranch(
                branch,
                targetTopY,
                junctionY,
                symbols,
                elements,
                issues);
        }

        double bottom =
            Math.Max(
                targetTopY +
                38,
                MainBusYmm + 40) +
            BottomMarginMm;

        elements.Insert(
            0,
            new RectangleSceneElement(
                new SceneId(
                    $"v2/{Safe(model.BoardUid.Value)}/frame"),
                new MmRect(
                    2,
                    2,
                    boardWidth - 4,
                    bottom - 4),
                SceneLayer.Background,
                0,
                SceneVisibility.Both,
                boardReference,
                Metadata(
                    "BoardFrame",
                    model.BoardUid.Value),
                "REFERENCE"));

        foreach (BoardDiagramBranchModel branch in model.Branches)
        {
            if (branch.NeutralPresence == BoardDiagramPresence.Unknown)
            {
                issues.Add(
                    new SceneIssue(
                        "NEUTRAL_TOPOLOGY_UNKNOWN",
                        SceneIssueSeverity.Warning,
                        $"Circuit '{branch.Code}' does not provide unequivocal neutral topology; neutral graphics were omitted.",
                        new EntityReference(
                            branch.CircuitUid,
                            EntityKind.Circuit)));
            }

            if (branch.ProtectiveEarthPresence == BoardDiagramPresence.Unknown)
            {
                issues.Add(
                    new SceneIssue(
                        "PE_TOPOLOGY_UNKNOWN",
                        SceneIssueSeverity.Warning,
                        $"Circuit '{branch.Code}' does not provide unequivocal PE topology; PE graphics were omitted.",
                        new EntityReference(
                            branch.CircuitUid,
                            EntityKind.Circuit)));
            }
        }

        return new DiagramScene(
            new SceneId(
                $"v2/{Safe(model.BoardUid.Value)}/scene"),
            DiagramSceneKind.BoardDetail,
            new MmRect(
                0,
                0,
                boardWidth,
                bottom),
            elements,
            new DiagramSceneMetadata(
                profile.ProfileId,
                profile.Version,
                DrawingProfileFingerprint.Compute(profile),
                LayoutVersion,
                fingerprint,
                fingerprint),
            [],
            issues
                .GroupBy(issue =>
                    string.Join(
                        "|",
                        issue.Code,
                        issue.Entity?.Kind.ToString(),
                        issue.Entity?.Uid.Value),
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray());
    }

    private static BranchGeometry[] PlanBranches(
        IReadOnlyList<BoardDiagramBranchModel> branches)
    {
        if (branches.Count == 0)
        {
            return [];
        }

        var result = new List<BranchGeometry>(branches.Count);
        double cursor = 0;

        foreach (BoardDiagramBranchModel branch in branches
                     .OrderBy(item => item.Number)
                     .ThenBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.CircuitUid.Value, StringComparer.Ordinal))
        {
            int targetCount = Math.Max(1, branch.Targets.Count);
            double halfWidth = Math.Max(
                BranchBaseHalfWidthMm,
                ((targetCount - 1) * TargetPitchMm / 2.0) +
                TargetHalfWidthMm);

            double axisX =
                result.Count == 0
                    ? halfWidth
                    : cursor +
                      BranchGapMm +
                      halfWidth;

            result.Add(
                new BranchGeometry(
                    branch,
                    axisX,
                    halfWidth));

            cursor =
                axisX +
                halfWidth;
        }

        return result.ToArray();
    }

    private static double ProtectionStackHeight(
        BoardDiagramBranchModel branch)
    {
        int count = 0;

        if (!string.IsNullOrWhiteSpace(branch.BreakerLabel))
        {
            count++;
        }

        if (branch.DifferentialEnabled)
        {
            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        return
            (count * 16.0) +
            ((count - 1) * ProtectionGapMm);
    }

    private static void AddBoardTitle(
        BoardDiagramModel model,
        double boardWidth,
        ICollection<SceneElement> elements)
    {
        EntityReference entity =
            new(model.BoardUid, EntityKind.Board);

        elements.Add(
            new TextSceneElement(
                new SceneId(
                    $"v2/{Safe(model.BoardUid.Value)}/title"),
                new MmRect(
                    MarginMm,
                    5,
                    boardWidth - (MarginMm * 2),
                    7),
                SceneLayer.Text,
                30,
                SceneVisibility.Both,
                entity,
                Metadata(
                    "BoardTitle",
                    model.BoardUid.Value),
                Display(
                    model.Code,
                    model.Name),
                "TITLE",
                SceneTextHorizontalAlignment.Center));
    }

    private static void AddMainBus(
        BoardDiagramModel model,
        EntityReference boardReference,
        double left,
        double right,
        IReadOnlyList<double> incomingXs,
        IReadOnlyList<BranchGeometry> branches,
        ICollection<SceneElement> elements)
    {
        string prefix =
            $"v2/{Safe(model.BoardUid.Value)}/main-bus";

        elements.Add(
            new LineSceneElement(
                new SceneId(
                    $"{prefix}/rail"),
                LineBounds(
                    new MmPoint(left, MainBusYmm),
                    new MmPoint(right, MainBusYmm)),
                SceneLayer.Power,
                15,
                SceneVisibility.Both,
                boardReference,
                Metadata(
                    "MainBus",
                    model.BoardUid.Value,
                    ("structuralOnly", "true")),
                new MmPoint(left, MainBusYmm),
                new MmPoint(right, MainBusYmm),
                "BUS"));

        var nodes = new List<(double X, string Id)>();

        for (int index = 0; index < incomingXs.Count; index++)
        {
            nodes.Add(
                (
                    incomingXs[index],
                    $"incoming-{index + 1:D2}"
                ));
        }

        foreach (BranchGeometry branch in branches)
        {
            nodes.Add(
                (
                    branch.AxisX,
                    $"branch-{Safe(branch.Branch.CircuitUid.Value)}"
                ));
        }

        foreach (IGrouping<double, (double X, string Id)> group in nodes
                     .GroupBy(item => item.X)
                     .OrderBy(group => group.Key))
        {
            string ids =
                string.Join(
                    "|",
                    group
                        .Select(item => item.Id)
                        .OrderBy(
                            value => value,
                            StringComparer.Ordinal));

            AddNode(
                $"{prefix}/node/{Safe(ids)}",
                new MmPoint(
                    group.Key,
                    MainBusYmm),
                boardReference,
                "BUS",
                "MainBusNode",
                elements);
        }
    }

    private static void AddIncomingSupplies(
        BoardDiagramModel model,
        IReadOnlyList<BoardDiagramIncomingModel> incomingSupplies,
        IReadOnlyList<double> incomingXs,
        EntityReference boardReference,
        ICollection<SceneElement> elements)
    {
        for (int index = 0;
             index < incomingSupplies.Count;
             index++)
        {
            BoardDiagramIncomingModel supply =
                incomingSupplies[index];
            double x =
                incomingXs[index];
            string style =
                supply.IsNormallyActive &&
                string.Equals(
                    supply.Role,
                    "NORMAL",
                    StringComparison.OrdinalIgnoreCase)
                    ? "POWER"
                    : "ALTERNATE_SUPPLY";
            EntityReference supplyReference =
                new(
                    supply.SupplyUid,
                    EntityKind.SupplyConnection);
            string prefix =
                $"v2/{Safe(model.BoardUid.Value)}/incoming/{Safe(supply.SupplyUid.Value)}";

            elements.Add(
                new LineSceneElement(
                    new SceneId(
                        $"{prefix}/line"),
                    LineBounds(
                        new MmPoint(x, IncomingTopYmm),
                        new MmPoint(x, MainBusYmm)),
                    SceneLayer.Power,
                    12,
                    SceneVisibility.Both,
                    supplyReference,
                    Metadata(
                        "IncomingSupply",
                        supply.SupplyUid.Value,
                        ("role", supply.Role),
                        (
                            "normallyActive",
                            supply.IsNormallyActive
                                ? "true"
                                : "false")),
                    new MmPoint(x, IncomingTopYmm),
                    new MmPoint(x, MainBusYmm),
                    style));

            elements.Add(
                new TextSceneElement(
                    new SceneId(
                        $"{prefix}/label"),
                    new MmRect(
                        x - 15,
                        12,
                        30,
                        5),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    supplyReference,
                    Metadata(
                        "IncomingSupplyLabel",
                        supply.SupplyUid.Value),
                    supply.OriginBoardCode,
                    "LABEL_SMALL",
                    SceneTextHorizontalAlignment.Center));

            if (!supply.IsNormallyActive)
            {
                elements.Add(
                    new TextSceneElement(
                        new SceneId(
                            $"{prefix}/state"),
                        new MmRect(
                            x - 15,
                            IncomingTopYmm + 2,
                            30,
                            4),
                        SceneLayer.Text,
                        30,
                        SceneVisibility.Both,
                        supplyReference,
                        Metadata(
                            "IncomingSupplyState",
                            supply.SupplyUid.Value),
                        "ALTERNATIVA",
                        "LABEL_SMALL",
                        SceneTextHorizontalAlignment.Center));
            }

        }
    }

    private static void AddBranch(
        BranchGeometry geometry,
        double targetTopY,
        double junctionY,
        SymbolCatalog symbols,
        ICollection<SceneElement> elements,
        ICollection<SceneIssue> issues)
    {
        BoardDiagramBranchModel branch =
            geometry.Branch;
        EntityReference circuitReference =
            new(
                branch.CircuitUid,
                EntityKind.Circuit);
        string prefix =
            $"v2/branch/{Safe(branch.CircuitUid.Value)}";
        double axisX =
            geometry.AxisX;

        double currentY =
            MainBusYmm;
        double nextY =
            MainBusYmm +
            ProtectionStartGapMm;

        AddVerticalLine(
            $"{prefix}/feed",
            axisX,
            currentY,
            nextY,
            circuitReference,
            "POWER",
            "CircuitPower",
            elements);
        currentY =
            nextY;

        if (!string.IsNullOrWhiteSpace(branch.BreakerLabel))
        {
            SymbolInstance breaker =
                symbols.Add(
                    BreakerSymbolId(branch.SystemCode),
                    new MmPoint(
                        axisX - 6,
                        currentY),
                    circuitReference,
                    new Dictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["RATING"] = branch.BreakerLabel
                    },
                    $"{prefix}/breaker",
                    "Breaker",
                    elements);

            currentY =
                breaker.Bounds.Bottom;

            if (branch.DifferentialEnabled)
            {
                AddVerticalLine(
                    $"{prefix}/between-protections",
                    axisX,
                    currentY,
                    currentY + ProtectionGapMm,
                    circuitReference,
                    "POWER",
                    "CircuitPower",
                    elements);
                currentY +=
                    ProtectionGapMm;
            }
        }

        if (branch.DifferentialEnabled)
        {
            SymbolInstance rcd =
                symbols.Add(
                    "RCD_2X",
                    new MmPoint(
                        axisX - 6,
                        currentY),
                    circuitReference,
                    new Dictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["RATING"] =
                            branch.DifferentialLabel ?? string.Empty
                    },
                    $"{prefix}/rcd",
                    "DifferentialProtection",
                    elements);

            currentY =
                rcd.Bounds.Bottom;
        }

        if (!string.IsNullOrWhiteSpace(branch.ConductorLabel))
        {
            elements.Add(
                new TextSceneElement(
                    new SceneId($"{prefix}/conductor"),
                    new MmRect(axisX + 3, currentY + 1, 45, 6),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    circuitReference,
                    Metadata("CircuitConductor", branch.CircuitUid.Value),
                    branch.ConductorLabel,
                    "LABEL_SMALL",
                    SceneTextHorizontalAlignment.Start));
        }

        BoardDiagramTargetModel[] targets =
            branch.Targets
                .OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Uid.Value, StringComparer.Ordinal)
                .ToArray();

        if (targets.Length == 0)
        {
            AddCircuitEndpoint(
                branch,
                axisX,
                currentY,
                targetTopY,
                symbols,
                elements);
        }
        else
        {

        double[] targetXs =
            CenteredAxes(
                targets.Length,
                axisX,
                TargetPitchMm);

        if (targets.Length > 1)
        {
            AddVerticalLine(
                $"{prefix}/to-junction",
                axisX,
                currentY,
                junctionY,
                circuitReference,
                "POWER",
                "CircuitPower",
                elements);

            double left =
                targetXs.Min();
            double right =
                targetXs.Max();

            elements.Add(
                new LineSceneElement(
                    new SceneId(
                        $"{prefix}/junction/rail"),
                    LineBounds(
                        new MmPoint(
                            left,
                            junctionY),
                        new MmPoint(
                            right,
                            junctionY)),
                    SceneLayer.Power,
                    15,
                    SceneVisibility.Both,
                    circuitReference,
                    Metadata(
                        "JunctionBus",
                        branch.CircuitUid.Value,
                        (
                            "structuralOnly",
                            "true")),
                    new MmPoint(
                        left,
                        junctionY),
                    new MmPoint(
                        right,
                        junctionY),
                    "BUS"));

            foreach (double nodeX in targetXs
                         .Append(axisX)
                         .Distinct()
                         .Order())
            {
                AddNode(
                    $"{prefix}/junction/node/{Coordinate(nodeX)}",
                    new MmPoint(
                        nodeX,
                        junctionY),
                    circuitReference,
                    "BUS",
                    "JunctionBusNode",
                    elements);
            }

            for (int index = 0;
                 index < targets.Length;
                 index++)
            {
                AddVerticalLine(
                    $"{prefix}/junction/out/{index + 1:D2}",
                    targetXs[index],
                    junctionY,
                    targetTopY,
                    circuitReference,
                    "POWER",
                    "CircuitPower",
                    elements);
            }
        }
        else
        {
            AddVerticalLine(
                $"{prefix}/to-target",
                axisX,
                currentY,
                targetTopY,
                circuitReference,
                "POWER",
                "CircuitPower",
                elements);
        }

        for (int index = 0;
             index < targets.Length;
             index++)
        {
            AddTarget(
                branch,
                targets[index],
                targetXs[index],
                targetTopY,
                index,
                symbols,
                elements);
        }
        }

        if (branch.NeutralPresence ==
            BoardDiagramPresence.Present)
        {
            issues.Add(
                new SceneIssue(
                    "NEUTRAL_PRESENT_NOT_YET_ROUTED_V2",
                    SceneIssueSeverity.Warning,
                    $"Circuit '{branch.Code}' declares a neutral conductor, but V2 power grammar does not route neutral until the canonical neutral topology contract is explicit.",
                    circuitReference));
        }

        if (branch.ProtectiveEarthPresence ==
            BoardDiagramPresence.Present)
        {
            issues.Add(
                new SceneIssue(
                    "PE_PRESENT_NOT_YET_ROUTED_V2",
                    SceneIssueSeverity.Warning,
                    $"Circuit '{branch.Code}' declares PE, but V2 power grammar does not route PE until the canonical PE topology contract is explicit.",
                    circuitReference));
        }
    }

    private static string BreakerSymbolId(string systemCode) =>
        systemCode.ToUpperInvariant() switch
        {
            "MONOFASICO" => "BREAKER_1X",
            "TRIFASICO" => "BREAKER_3X",
            _ => "BREAKER",
        };

    private static void AddCircuitEndpoint(
        BoardDiagramBranchModel branch,
        double axisX,
        double currentY,
        double targetTopY,
        SymbolCatalog symbols,
        ICollection<SceneElement> elements)
    {
        EntityReference reference =
            new(
                branch.CircuitUid,
                EntityKind.Circuit);
        string prefix =
            $"v2/branch/{Safe(branch.CircuitUid.Value)}";

        AddVerticalLine(
            $"{prefix}/to-endpoint",
            axisX,
            currentY,
            targetTopY,
            reference,
            "POWER",
            "CircuitPower",
            elements);

        symbols.Add(
            "CIRCUIT_MARKER",
            new MmPoint(
                axisX - 8,
                targetTopY),
            reference,
            new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["NUMBER"] =
                    branch.Number.ToString(
                        CultureInfo.InvariantCulture)
            },
            $"{prefix}/endpoint",
            "FinalLoad",
            elements);

        if (!string.IsNullOrWhiteSpace(
                branch.Descriptor))
        {
            elements.Add(
                new TextSceneElement(
                    new SceneId(
                        $"{prefix}/endpoint/descriptor"),
                    new MmRect(
                        axisX - 15,
                        targetTopY + 18,
                        30,
                        8),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    reference,
                    Metadata(
                        "CircuitDescriptor",
                        branch.CircuitUid.Value),
                    branch.Descriptor,
                    "LABEL_SMALL",
                    SceneTextHorizontalAlignment.Center));
        }
    }

    private static void AddMainProtection(
        BoardDiagramModel model,
        double centerX,
        SymbolCatalog symbols,
        ICollection<SceneElement> elements)
    {
        string symbolId =
            model.MainProtectionPoles switch
            {
                2 => "BREAKER_2X",
                4 => "BREAKER_4X",
                _ => "BREAKER",
            };

        symbols.Add(
            symbolId,
            new MmPoint(
                centerX - 6,
                MainBusYmm - 22),
            new EntityReference(
                model.BoardUid,
                EntityKind.Board),
            new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["RATING"] =
                    model.MainProtectionLabel ??
                    string.Empty
            },
            $"v2/{Safe(model.BoardUid.Value)}/main-protection",
            "MainProtection",
            elements);
    }

    private static void AddTarget(
        BoardDiagramBranchModel branch,
        BoardDiagramTargetModel target,
        double centerX,
        double topY,
        int index,
        SymbolCatalog symbols,
        ICollection<SceneElement> elements)
    {
        EntityReference reference =
            new(
                target.Uid,
                target.EntityKind);
        string prefix =
            $"v2/branch/{Safe(branch.CircuitUid.Value)}/target/{index + 1:D2}-{Safe(target.Uid.Value)}";

        switch (target.Kind)
        {
            case BoardDiagramTargetKind.DownstreamBoard:
                symbols.Add(
                    "DOWNSTREAM_BOARD",
                    new MmPoint(
                        centerX - 11,
                        topY),
                    reference,
                    new Dictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["NAME"] =
                            Display(
                                target.Code,
                                target.Name)
                    },
                    prefix,
                    "DownstreamBoard",
                    elements);
                break;

            case BoardDiagramTargetKind.FinalLoad:
                symbols.Add(
                    "CIRCUIT_MARKER",
                    new MmPoint(
                        centerX - 8,
                        topY),
                    reference,
                    new Dictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["NUMBER"] =
                            branch.Number.ToString(
                                CultureInfo.InvariantCulture)
                    },
                    prefix,
                    "FinalLoad",
                    elements);

                elements.Add(
                    new TextSceneElement(
                        new SceneId(
                            $"{prefix}/name"),
                        new MmRect(
                            centerX - 15,
                            topY + 18,
                            30,
                            8),
                        SceneLayer.Text,
                        30,
                        SceneVisibility.Both,
                        reference,
                        Metadata(
                            "FinalLoadLabel",
                            target.Uid.Value),
                        Display(
                            target.Code,
                            target.Name),
                        "LABEL_SMALL",
                        SceneTextHorizontalAlignment.Center));
                break;

            default:
                symbols.Add(
                    "UNKNOWN_ENDPOINT",
                    new MmPoint(
                        centerX - 9,
                        topY),
                    reference,
                    new Dictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["NAME"] =
                            Display(
                                target.Code,
                                target.Name)
                    },
                    prefix,
                    "UnknownDestination",
                    elements);
                break;
        }
    }

    private static void AddVerticalLine(
        string id,
        double x,
        double firstY,
        double secondY,
        EntityReference reference,
        string lineStyleId,
        string semanticRole,
        ICollection<SceneElement> elements)
    {
        if (Math.Abs(
                firstY -
                secondY) <
            double.Epsilon)
        {
            return;
        }

        MmPoint start =
            new(
                x,
                Math.Min(
                    firstY,
                    secondY));
        MmPoint end =
            new(
                x,
                Math.Max(
                    firstY,
                    secondY));

        elements.Add(
            new LineSceneElement(
                new SceneId(id),
                LineBounds(
                    start,
                    end),
                SceneLayer.Power,
                12,
                SceneVisibility.Both,
                reference,
                Metadata(
                    semanticRole,
                    reference.Uid.Value),
                start,
                end,
                lineStyleId));
    }

    private static void AddNode(
        string id,
        MmPoint point,
        EntityReference reference,
        string lineStyleId,
        string semanticRole,
        ICollection<SceneElement> elements)
    {
        elements.Add(
            new CircleSceneElement(
                new SceneId(id),
                new MmRect(
                    point.X -
                    NodeRadiusMm,
                    point.Y -
                    NodeRadiusMm,
                    NodeRadiusMm * 2,
                    NodeRadiusMm * 2),
                SceneLayer.Power,
                20,
                SceneVisibility.Both,
                reference,
                Metadata(
                    semanticRole,
                    reference.Uid.Value),
                point,
                NodeRadiusMm,
                lineStyleId));
    }

    private static double[] CenteredAxes(
        int count,
        double center,
        double pitch)
    {
        if (count <= 0)
        {
            return [];
        }

        double first =
            center -
            (((count - 1) * pitch) / 2.0);

        return Enumerable
            .Range(
                0,
                count)
            .Select(index =>
                first +
                (index * pitch))
            .ToArray();
    }

    private static MmRect LineBounds(
        MmPoint first,
        MmPoint second)
    {
        double left =
            Math.Min(
                first.X,
                second.X);
        double top =
            Math.Min(
                first.Y,
                second.Y);
        double width =
            Math.Max(
                Math.Abs(
                    second.X -
                    first.X),
                MinimumPrimitiveExtentMm);
        double height =
            Math.Max(
                Math.Abs(
                    second.Y -
                    first.Y),
                MinimumPrimitiveExtentMm);

        return new MmRect(
            left,
            top,
            width,
            height);
    }

    private static IReadOnlyDictionary<string, string> Metadata(
        string role,
        string entityUid,
        params (string Key, string Value)[] values)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                ["v2Role"] = role,
                ["entityUid"] = entityUid
            };

        foreach ((string key, string value) in values)
        {
            result[key] =
                value;
        }

        return result;
    }

    private static string Display(
        string code,
        string name)
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

    private static string Fingerprint(
        BoardDiagramModel model)
    {
        var builder =
            new StringBuilder();

        Append(
            builder,
            model.BoardUid.Value);
        Append(
            builder,
            model.Code);
        Append(
            builder,
            model.Name);
        Append(
            builder,
            model.State);

        foreach (BoardDiagramIncomingModel incoming in
                 model.IncomingSupplies
                     .OrderBy(
                         item => item.SupplyUid.Value,
                         StringComparer.Ordinal))
        {
            Append(
                builder,
                "IN");
            Append(
                builder,
                incoming.SupplyUid.Value);
            Append(
                builder,
                incoming.FeederCircuitUid.Value);
            Append(
                builder,
                incoming.OriginBoardUid.Value);
            Append(
                builder,
                incoming.Role);
            Append(
                builder,
                incoming.IsNormallyActive
                    ? "1"
                    : "0");
            Append(
                builder,
                incoming.State);
        }

        foreach (BoardDiagramBranchModel branch in
                 model.Branches
                     .OrderBy(item => item.Number)
                     .ThenBy(
                         item => item.CircuitUid.Value,
                         StringComparer.Ordinal))
        {
            Append(
                builder,
                "BR");
            Append(
                builder,
                branch.CircuitUid.Value);
            Append(
                builder,
                branch.Number.ToString(
                    CultureInfo.InvariantCulture));
            Append(
                builder,
                branch.Code);
            Append(
                builder,
                branch.Name);
            Append(
                builder,
                branch.SystemCode);
            Append(
                builder,
                branch.State);
            Append(
                builder,
                branch.DataState);
            Append(
                builder,
                branch.BreakerLabel);
            Append(
                builder,
                branch.DifferentialLabel);
            Append(
                builder,
                branch.ConductorLabel);
            Append(
                builder,
                branch.NeutralPresence.ToString());
            Append(
                builder,
                branch.NeutralLabel);
            Append(
                builder,
                branch.ProtectiveEarthPresence.ToString());
            Append(
                builder,
                branch.ResultStatus);

            foreach (BoardDiagramTargetModel target in
                     branch.Targets
                         .OrderBy(
                             item => item.Code,
                             StringComparer.OrdinalIgnoreCase)
                         .ThenBy(
                             item => item.Uid.Value,
                             StringComparer.Ordinal))
            {
                Append(
                    builder,
                    "TARGET");
                Append(
                    builder,
                    target.Uid.Value);
                Append(
                    builder,
                    target.EntityKind.ToString());
                Append(
                    builder,
                    target.Kind.ToString());
                Append(
                    builder,
                    target.Code);
                Append(
                    builder,
                    target.Name);
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    builder.ToString())));
    }

    private static void Append(
        StringBuilder builder,
        string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder
            .Append(
                value.Length.ToString(
                    CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }

    private static string Safe(
        string value)
    {
        var builder =
            new StringBuilder(
                value.Length);

        foreach (char character in value)
        {
            builder.Append(
                char.IsLetterOrDigit(
                    character) ||
                character is '-' or '_' or '.'
                    ? character
                    : '-');
        }

        return builder.Length == 0
            ? "unknown"
            : builder.ToString();
    }

    private static string Coordinate(
        double value) =>
        value
            .ToString(
                "0.###",
                CultureInfo.InvariantCulture)
            .Replace(
                '.',
                '-');

    private sealed record BranchGeometry(
        BoardDiagramBranchModel Branch,
        double AxisX,
        double HalfWidth);

    private sealed record SymbolInstance(
        MmRect Bounds,
        IReadOnlyList<SceneAnchor> Anchors);

    private sealed class SymbolCatalog
    {
        private readonly IReadOnlyDictionary<string, SymbolDefinition> symbols;
        private readonly IReadOnlyDictionary<string, LineStyleDefinition> lineStyles;

        public SymbolCatalog(
            RIC18DrawingProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            symbols =
                profile.Symbols.ToDictionary(
                    item => item.Id,
                    StringComparer.Ordinal);
            lineStyles =
                profile.LineStyles.ToDictionary(
                    item => item.Id,
                    StringComparer.Ordinal);
        }

        public SymbolInstance Add(
            string symbolId,
            MmPoint origin,
            EntityReference reference,
            IReadOnlyDictionary<string, string> labels,
            string idPrefix,
            string semanticRole,
            ICollection<SceneElement> elements)
        {
            if (!symbols.TryGetValue(
                    symbolId,
                    out SymbolDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Drawing profile is missing symbol '{symbolId}'.");
            }

            MmRect bounds =
                Translate(
                    definition.NominalBounds,
                    origin);
            var children =
                new List<SceneId>();

            for (int index = 0;
                 index < definition.Primitives.Count;
                 index++)
            {
                SceneElement primitive =
                    ExpandPrimitive(
                        definition.Primitives[index],
                        index,
                        origin,
                        bounds,
                        reference,
                        idPrefix,
                        semanticRole);
                elements.Add(
                    primitive);
                children.Add(
                    primitive.Id);
            }

            foreach (LabelSlot slot in
                     definition.LabelSlots
                         .OrderBy(
                             item => item.Priority)
                         .ThenBy(
                             item => item.Id,
                             StringComparer.Ordinal))
            {
                if (!labels.TryGetValue(
                        slot.Id,
                        out string? text) ||
                    string.IsNullOrWhiteSpace(
                        text))
                {
                    if (slot.Required)
                    {
                        text =
                            "—";
                    }
                    else
                    {
                        continue;
                    }
                }

                SceneId textId =
                    new(
                        $"{idPrefix}/label/{Safe(slot.Id)}");
                elements.Add(
                    new TextSceneElement(
                        textId,
                        Translate(
                            slot.Bounds,
                            origin),
                        SceneLayer.Text,
                        30,
                        SceneVisibility.Both,
                        reference,
                        Metadata(
                            semanticRole,
                            reference.Uid.Value,
                            (
                                "symbolId",
                                definition.Id),
                            (
                                "labelSlot",
                                slot.Id)),
                        text,
                        slot.TextStyleId,
                        SceneTextHorizontalAlignment.Center,
                        SceneTextVerticalAlignment.Center));
                children.Add(
                    textId);
            }

            SceneAnchor[] anchors =
                definition.Anchors
                    .Select(anchor =>
                        new SceneAnchor(
                            anchor.Id,
                            anchor.Role,
                            Translate(
                                anchor.Point,
                                origin),
                            anchor.Direction))
                    .ToArray();

            elements.Add(
                new GroupSceneElement(
                    new SceneId(
                        $"{idPrefix}/group"),
                    bounds,
                    SceneLayer.Symbol,
                    5,
                    SceneVisibility.Both,
                    reference,
                    Metadata(
                        semanticRole,
                        reference.Uid.Value,
                        (
                            "symbolId",
                            definition.Id)),
                    children,
                    anchors));

            return new SymbolInstance(
                bounds,
                anchors);
        }

        private SceneElement ExpandPrimitive(
            SymbolPrimitive primitive,
            int index,
            MmPoint origin,
            MmRect symbolBounds,
            EntityReference reference,
            string idPrefix,
            string semanticRole)
        {
            SceneId id =
                new(
                    $"{idPrefix}/primitive/{index:D3}");
            SceneLayer layer =
                LayerFor(
                    primitive.LineStyleId);
            IReadOnlyDictionary<string, string> metadata =
                Metadata(
                    semanticRole,
                    reference.Uid.Value);

            return primitive switch
            {
                LineSymbolPrimitive line =>
                    new LineSceneElement(
                        id,
                        LineBounds(
                            Translate(
                                line.Start,
                                origin),
                            Translate(
                                line.End,
                                origin)),
                        layer,
                        15,
                        SceneVisibility.Both,
                        reference,
                        metadata,
                        Translate(
                            line.Start,
                            origin),
                        Translate(
                            line.End,
                            origin),
                        line.LineStyleId),

                PolylineSymbolPrimitive polyline =>
                    CreatePolyline(
                        id,
                        polyline,
                        origin,
                        layer,
                        reference,
                        metadata),

                RectangleSymbolPrimitive rectangle =>
                    new RectangleSceneElement(
                        id,
                        Translate(
                            rectangle.Rectangle,
                            origin),
                        layer,
                        15,
                        SceneVisibility.Both,
                        reference,
                        metadata,
                        rectangle.LineStyleId),

                CircleSymbolPrimitive circle =>
                    new CircleSceneElement(
                        id,
                        new MmRect(
                            origin.X +
                            circle.Center.X -
                            circle.Radius,
                            origin.Y +
                            circle.Center.Y -
                            circle.Radius,
                            circle.Radius * 2,
                            circle.Radius * 2),
                        layer,
                        15,
                        SceneVisibility.Both,
                        reference,
                        metadata,
                        Translate(
                            circle.Center,
                            origin),
                        circle.Radius,
                        circle.LineStyleId),

                ArcSymbolPrimitive arc =>
                    CreateArc(
                        id,
                        arc,
                        origin,
                        layer,
                        reference,
                        metadata),

                PathSymbolPrimitive _ =>
                    throw new InvalidOperationException(
                        $"V2 symbol '{idPrefix}' uses a path primitive that cannot be translated safely by the direct scene builder."),

                _ =>
                    throw new InvalidOperationException(
                        $"Unsupported symbol primitive '{primitive.GetType().FullName}'.")
            };
        }

        private SceneLayer LayerFor(
            string lineStyleId)
        {
            if (!lineStyles.TryGetValue(
                    lineStyleId,
                    out LineStyleDefinition? style))
            {
                throw new InvalidOperationException(
                    $"Drawing profile is missing line style '{lineStyleId}'.");
            }

            return style.Role switch
            {
                LineSemanticRole.Ground =>
                    SceneLayer.Grounding,
                LineSemanticRole.Annotation =>
                    SceneLayer.Annotation,
                LineSemanticRole.Reference or
                LineSemanticRole.Boundary =>
                    SceneLayer.Background,
                _ =>
                    SceneLayer.Power
            };
        }

        private static PolylineSceneElement CreatePolyline(
            SceneId id,
            PolylineSymbolPrimitive polyline,
            MmPoint origin,
            SceneLayer layer,
            EntityReference reference,
            IReadOnlyDictionary<string, string> metadata)
        {
            MmPoint[] points =
                polyline.Points
                    .Select(point =>
                        Translate(
                            point,
                            origin))
                    .ToArray();

            return new PolylineSceneElement(
                id,
                BoundsFor(
                    points),
                layer,
                15,
                SceneVisibility.Both,
                reference,
                metadata,
                points,
                polyline.LineStyleId);
        }

        private static PathSceneElement CreateArc(
            SceneId id,
            ArcSymbolPrimitive arc,
            MmPoint origin,
            SceneLayer layer,
            EntityReference reference,
            IReadOnlyDictionary<string, string> metadata)
        {
            MmPoint center =
                Translate(
                    arc.Center,
                    origin);
            double startRadians =
                arc.StartDegrees *
                Math.PI /
                180.0;
            double endRadians =
                (
                    arc.StartDegrees +
                    arc.SweepDegrees
                ) *
                Math.PI /
                180.0;
            double startX =
                center.X +
                (
                    arc.Radius *
                    Math.Cos(
                        startRadians)
                );
            double startY =
                center.Y +
                (
                    arc.Radius *
                    Math.Sin(
                        startRadians)
                );
            double endX =
                center.X +
                (
                    arc.Radius *
                    Math.Cos(
                        endRadians)
                );
            double endY =
                center.Y +
                (
                    arc.Radius *
                    Math.Sin(
                        endRadians)
                );
            int largeArc =
                Math.Abs(
                    arc.SweepDegrees) >
                180
                    ? 1
                    : 0;
            int sweep =
                arc.SweepDegrees >= 0
                    ? 1
                    : 0;

            string data =
                FormattableString.Invariant(
                    $"M {startX:R} {startY:R} A {arc.Radius:R} {arc.Radius:R} 0 {largeArc} {sweep} {endX:R} {endY:R}");

            return new PathSceneElement(
                id,
                new MmRect(
                    center.X -
                    arc.Radius,
                    center.Y -
                    arc.Radius,
                    arc.Radius * 2,
                    arc.Radius * 2),
                layer,
                15,
                SceneVisibility.Both,
                reference,
                metadata,
                data,
                arc.LineStyleId);
        }

        private static MmRect BoundsFor(
            IReadOnlyList<MmPoint> points)
        {
            double left =
                points.Min(item => item.X);
            double top =
                points.Min(item => item.Y);
            double right =
                points.Max(item => item.X);
            double bottom =
                points.Max(item => item.Y);

            return new MmRect(
                left,
                top,
                Math.Max(
                    right -
                    left,
                    MinimumPrimitiveExtentMm),
                Math.Max(
                    bottom -
                    top,
                    MinimumPrimitiveExtentMm));
        }

        private static MmRect Translate(
            MmRect value,
            MmPoint origin) =>
            new(
                value.X +
                origin.X,
                value.Y +
                origin.Y,
                value.Width,
                value.Height);

        private static MmPoint Translate(
            MmPoint value,
            MmPoint origin) =>
            new(
                value.X +
                origin.X,
                value.Y +
                origin.Y);
    }
}
