using System.Globalization;
using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed record PositionedCompositionBlock
{
    public PositionedCompositionBlock(string blockId, MmRect bounds)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException("Composition block ID is required.", nameof(blockId));
        }

        BlockId = blockId;
        Bounds = bounds;
    }

    public string BlockId { get; }

    public MmRect Bounds { get; }
}

public sealed class SceneAssemblyInput
{
    public SceneAssemblyInput(
        DrawingComposition composition,
        IEnumerable<PositionedCompositionBlock> blocks,
        MmRect sceneBounds,
        string projectionFingerprint,
        string layoutEngineVersion)
        : this(
            composition,
            blocks,
            sceneBounds,
            projectionFingerprint,
            layoutEngineVersion,
            new SceneId("scene/assembled"),
            DiagramSceneKind.Unknown,
            [])
    {
    }

    public SceneAssemblyInput(
        DrawingComposition composition,
        IEnumerable<PositionedCompositionBlock> blocks,
        MmRect sceneBounds,
        string projectionFingerprint,
        string layoutEngineVersion,
        SceneId sceneId,
        DiagramSceneKind sceneKind,
        IEnumerable<SceneIssue> issues)
    {
        Composition = composition ??
            throw new ArgumentNullException(nameof(composition));
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(issues);

        if (string.IsNullOrWhiteSpace(projectionFingerprint))
        {
            throw new ArgumentException(
                "Projection fingerprint is required.",
                nameof(projectionFingerprint));
        }

        if (string.IsNullOrWhiteSpace(layoutEngineVersion))
        {
            throw new ArgumentException(
                "Layout engine version is required.",
                nameof(layoutEngineVersion));
        }

        Blocks = Array.AsReadOnly(blocks.ToArray());
        SceneBounds = sceneBounds;
        ProjectionFingerprint = projectionFingerprint;
        LayoutEngineVersion = layoutEngineVersion;
        SceneId = sceneId;
        SceneKind = sceneKind;
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public DrawingComposition Composition { get; }

    public IReadOnlyList<PositionedCompositionBlock> Blocks { get; }

    public MmRect SceneBounds { get; }

    public string ProjectionFingerprint { get; }

    public string LayoutEngineVersion { get; }

    public SceneId SceneId { get; }

    public DiagramSceneKind SceneKind { get; }

    public IReadOnlyList<SceneIssue> Issues { get; }
}

public sealed class SceneAssembly
{
    private const double MinimumPrimitiveExtentMm = 0.001;

    public DiagramScene Assemble(
        SceneAssemblyInput input,
        RIC18DrawingProfile profile) =>
        Assemble(
            input,
            profile,
            measurement: null);

    public DiagramScene Assemble(
        SceneAssemblyInput input,
        RIC18DrawingProfile profile,
        CompositionMeasurement? measurement)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(profile);

        string actualProfileFingerprint =
            DrawingProfileFingerprint.Compute(profile);

        if (!string.Equals(
                input.Composition.ProfileFingerprint,
                actualProfileFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Composition profile fingerprint does not match the supplied drawing profile.");
        }

        Dictionary<string, PositionedCompositionBlock> positions =
            IndexPositions(input.Blocks);
        Dictionary<string, BlockDefinition> blocks =
            profile.Blocks.ToDictionary(
                block => block.Id,
                StringComparer.Ordinal);
        Dictionary<string, SymbolDefinition> symbols =
            profile.Symbols.ToDictionary(
                symbol => symbol.Id,
                StringComparer.Ordinal);
        Dictionary<string, LineStyleDefinition> lineStyles =
            profile.LineStyles.ToDictionary(
                style => style.Id,
                StringComparer.Ordinal);

        ValidatePositionCoverage(input.Composition, positions);

        var elements = new List<SceneElement>();

        foreach (CompositionBlock block in input.Composition.Blocks
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            PositionedCompositionBlock positioned = positions[block.Id];

            if (!blocks.TryGetValue(
                    block.BlockDefinitionId,
                    out BlockDefinition? definition))
            {
                throw new InvalidOperationException(
                    $"Drawing profile is missing block definition '{block.BlockDefinitionId}'.");
            }

            MeasuredBlock? measuredBlock =
                measurement is null
                    ? null
                    : measurement.GetBlock(block.Id);

            if (block.SemanticRole == "CircuitBranch")
            {
                AssembleCircuitBranch(
                    block,
                    positioned,
                    definition,
                    elements);
            }
            else if (block.SemanticRole == "BoardFrame")
            {
                AssembleBoardFrame(
                    block,
                    positioned,
                    definition,
                    elements);
            }
            else if (block.SemanticRole is "MainBus" or "NeutralBus" or "ProtectiveEarthBus")
            {
                AssembleStructuralRail(
                    block,
                    positioned,
                    definition,
                    elements);
            }
            else
            {
                AssembleBlock(
                    block,
                    positioned,
                    definition,
                    symbols,
                    lineStyles,
                    measuredBlock,
                    elements);
            }
        }

        Dictionary<string, GroupSceneElement> groups = elements
            .OfType<GroupSceneElement>()
            .ToDictionary(
                group => group.Id.Value,
                StringComparer.Ordinal);

        SceneConnection[] connections = input.Composition.Connections
            .OrderBy(connection => connection.Id, StringComparer.Ordinal)
            .Select(connection => AssembleConnection(
                connection,
                groups,
                lineStyles))
            .ToArray();

        var metadata = new DiagramSceneMetadata(
            profile.ProfileId,
            profile.Version,
            actualProfileFingerprint,
            input.LayoutEngineVersion,
            input.Composition.InputFingerprint,
            input.ProjectionFingerprint);

        return new DiagramScene(
            input.SceneId,
            input.SceneKind,
            input.SceneBounds,
            elements,
            metadata,
            connections,
            input.Issues);
    }

    private static Dictionary<string, PositionedCompositionBlock> IndexPositions(
        IEnumerable<PositionedCompositionBlock> positions)
    {
        var result = new Dictionary<string, PositionedCompositionBlock>(
            StringComparer.Ordinal);

        foreach (PositionedCompositionBlock position in positions)
        {
            if (!result.TryAdd(position.BlockId, position))
            {
                throw new InvalidOperationException(
                    $"Composition block '{position.BlockId}' has more than one supplied position.");
            }
        }

        return result;
    }

    private static void ValidatePositionCoverage(
        DrawingComposition composition,
        IReadOnlyDictionary<string, PositionedCompositionBlock> positions)
    {
        foreach (CompositionBlock block in composition.Blocks)
        {
            if (!positions.ContainsKey(block.Id))
            {
                throw new InvalidOperationException(
                    $"Composition block '{block.Id}' has no supplied position.");
            }
        }

        HashSet<string> compositionIds = composition.Blocks
            .Select(block => block.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string positionedId in positions.Keys)
        {
            if (!compositionIds.Contains(positionedId))
            {
                throw new InvalidOperationException(
                    $"Position was supplied for unknown composition block '{positionedId}'.");
            }
        }
    }

    private static void AssembleCircuitBranch(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        ICollection<SceneElement> output)
    {
        double centerX =
            positioned.Bounds.X +
            (positioned.Bounds.Width / 2.0);
        MmPoint start =
            new(
                centerX,
                positioned.Bounds.Y);
        MmPoint end =
            new(
                centerX,
                positioned.Bounds.Bottom);
        SceneId axisId =
            new($"{block.Id}/axis");

        output.Add(
            new LineSceneElement(
                axisId,
                BoundsFor([start, end]),
                SceneLayer.Power,
                15,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                start,
                end,
                "POWER"));

        output.Add(
            new GroupSceneElement(
                new SceneId(block.Id),
                positioned.Bounds,
                SceneLayer.Symbol,
                5,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                [axisId],
                [
                    new SceneAnchor(
                        "IN",
                        AnchorRole.PowerIn,
                        start,
                        AnchorDirection.Up),
                    new SceneAnchor(
                        "OUT",
                        AnchorRole.PowerOut,
                        end,
                        AnchorDirection.Down)
                ]));
    }

    private static void AssembleBoardFrame(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        ICollection<SceneElement> output)
    {
        SceneId frameId =
            new($"{block.Id}/frame");

        output.Add(
            new RectangleSceneElement(
                frameId,
                positioned.Bounds,
                SceneLayer.Annotation,
                1,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                "ANNOTATION"));

        var children =
            new List<SceneId>
            {
                frameId
            };

        if (block.Labels.TryGetValue("CODE", out string? code) &&
            !string.IsNullOrWhiteSpace(code))
        {
            SceneId id =
                new($"{block.Id}/label/code");

            output.Add(
                new TextSceneElement(
                    id,
                    new MmRect(
                        positioned.Bounds.X + 2,
                        positioned.Bounds.Y + 1.5,
                        28,
                        4),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    block.Entity,
                    GroupMetadata(block, definition.Id),
                    code,
                    "TECH"));

            children.Add(id);
        }

        if (block.Labels.TryGetValue("NAME", out string? name) &&
            !string.IsNullOrWhiteSpace(name))
        {
            SceneId id =
                new($"{block.Id}/label/name");

            output.Add(
                new TextSceneElement(
                    id,
                    new MmRect(
                        positioned.Bounds.X + 2,
                        positioned.Bounds.Y + 5.5,
                        Math.Max(
                            positioned.Bounds.Width - 4,
                            MinimumPrimitiveExtentMm),
                        4),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    block.Entity,
                    GroupMetadata(block, definition.Id),
                    name,
                    "LABEL_SMALL"));

            children.Add(id);
        }

        output.Add(
            new GroupSceneElement(
                new SceneId(block.Id),
                positioned.Bounds,
                SceneLayer.Symbol,
                0,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                children));
    }

    private static void AssembleStructuralRail(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        ICollection<SceneElement> output)
    {
        string lineStyleId =
            block.SemanticRole switch
            {
                "MainBus" => "BUS",
                "NeutralBus" => "BUS",
                "ProtectiveEarthBus" => "GROUND",
                _ => throw new InvalidOperationException(
                    $"Unsupported structural rail role '{block.SemanticRole}'.")
            };
        AnchorRole anchorRole =
            block.SemanticRole switch
            {
                "MainBus" => AnchorRole.BusTap,
                "NeutralBus" => AnchorRole.Neutral,
                "ProtectiveEarthBus" => AnchorRole.Ground,
                _ => throw new InvalidOperationException(
                    $"Unsupported structural rail role '{block.SemanticRole}'.")
            };
        SceneLayer layer =
            block.SemanticRole == "ProtectiveEarthBus"
                ? SceneLayer.Grounding
                : SceneLayer.Power;

        double railInset =
            block.SemanticRole == "MainBus"
                ? 0
                : 4;
        double left =
            positioned.Bounds.X + railInset;
        double right =
            positioned.Bounds.Right - railInset;
        double y =
            positioned.Bounds.Y +
            (positioned.Bounds.Height / 2.0);

        SceneId railId =
            new($"{block.Id}/rail");

        var rail = new LineSceneElement(
            railId,
            new MmRect(
                left,
                y,
                Math.Max(
                    right - left,
                    MinimumPrimitiveExtentMm),
                MinimumPrimitiveExtentMm),
            layer,
            15,
            SceneVisibility.Both,
            block.Entity,
            GroupMetadata(block, definition.Id),
            new MmPoint(left, y),
            new MmPoint(right, y),
            lineStyleId);

        output.Add(rail);

        string[] taps =
            block.Labels.TryGetValue("TAPS", out string? raw) &&
            !string.IsNullOrWhiteSpace(raw)
                ? raw.Split(
                    '|',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                : [];

        var anchors =
            new List<SceneAnchor>();

        if (block.SemanticRole == "MainBus")
        {
            anchors.Add(
                new SceneAnchor(
                    "IN",
                    AnchorRole.PowerIn,
                    new MmPoint(
                        left + ((right - left) / 2.0),
                        y),
                    AnchorDirection.Up));
            anchors.Add(
                new SceneAnchor(
                    "OUT",
                    AnchorRole.PowerOut,
                    new MmPoint(right, y),
                    AnchorDirection.Right));
        }
        else
        {
            anchors.Add(
                new SceneAnchor(
                    "IN",
                    anchorRole,
                    new MmPoint(left, y),
                    AnchorDirection.Left));
            anchors.Add(
                new SceneAnchor(
                    "OUT",
                    anchorRole,
                    new MmPoint(right, y),
                    AnchorDirection.Right));
        }

        for (int index = 0; index < taps.Length; index++)
        {
            double x;

            if (block.SemanticRole == "MainBus")
            {
                // Layout owns branch-column geometry. The bus tap must be
                // collinear with the corresponding branch, while the incoming
                // feeder retains the centre node.
                double centerX =
                    left + ((right - left) / 2.0);
                double slotWidth =
                    (right - left) /
                    Math.Max(1, taps.Length);
                double relativeSlot =
                    index - ((taps.Length - 1) / 2.0);

                if (relativeSlot >= 0)
                {
                    relativeSlot += 1.0;
                }

                x =
                    centerX +
                    (relativeSlot * slotWidth);
            }
            else
            {
                double fraction =
                    (index + 1.0) /
                    (taps.Length + 1.0);
                x =
                    left +
                    ((right - left) * fraction);
            }

            anchors.Add(
                new SceneAnchor(
                    $"TAP:{taps[index]}",
                    block.SemanticRole == "MainBus"
                        ? AnchorRole.BusTap
                        : anchorRole,
                    new MmPoint(x, y),
                    AnchorDirection.Down));
        }

        var children =
            new List<SceneId>
            {
                railId
            };

        if (block.Labels.TryGetValue("LABEL", out string? label) &&
            !string.IsNullOrWhiteSpace(label))
        {
            SceneId labelId =
                new($"{block.Id}/label");

            output.Add(
                new TextSceneElement(
                    labelId,
                    new MmRect(
                        positioned.Bounds.X,
                        Math.Max(
                            0,
                            positioned.Bounds.Y - 4),
                        positioned.Bounds.Width,
                        4),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    block.Entity,
                    GroupMetadata(block, definition.Id),
                    label,
                    "TECH"));

            children.Add(labelId);
        }

        output.Add(
            new GroupSceneElement(
                new SceneId(block.Id),
                positioned.Bounds,
                SceneLayer.Symbol,
                5,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                children,
                anchors));
    }

    private static void AssembleServiceEntranceFrame(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        ICollection<SceneElement> output,
        ICollection<SceneId> children)
    {
        const double inset = 2;
        const double headerHeight = 10;
        MmRect frameBounds =
            new(
                positioned.Bounds.X + inset,
                positioned.Bounds.Y + inset,
                Math.Max(
                    positioned.Bounds.Width - (inset * 2),
                    MinimumPrimitiveExtentMm),
                Math.Max(
                    positioned.Bounds.Height - (inset * 2),
                    MinimumPrimitiveExtentMm));
        SceneId frameId =
            new($"{block.Id}/service-frame");

        output.Add(
            new RectangleSceneElement(
                frameId,
                frameBounds,
                SceneLayer.Symbol,
                15,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                "POWER"));
        children.Add(frameId);

        SceneId headerId =
            new($"{block.Id}/service-header");
        MmPoint headerStart =
            new(
                frameBounds.X,
                frameBounds.Y + headerHeight);
        MmPoint headerEnd =
            new(
                frameBounds.Right,
                frameBounds.Y + headerHeight);

        output.Add(
            new LineSceneElement(
                headerId,
                BoundsFor([headerStart, headerEnd]),
                SceneLayer.Power,
                15,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                headerStart,
                headerEnd,
                "POWER"));
        children.Add(headerId);

        if (block.Labels.TryGetValue(
                "TITLE",
                out string? title) &&
            !string.IsNullOrWhiteSpace(title))
        {
            SceneId titleId =
                new($"{block.Id}/service-title");

            output.Add(
                new TextSceneElement(
                    titleId,
                    new MmRect(
                        frameBounds.X + 2,
                        frameBounds.Y + 2,
                        Math.Max(
                            frameBounds.Width - 4,
                            MinimumPrimitiveExtentMm),
                        5),
                    SceneLayer.Text,
                    30,
                    SceneVisibility.Both,
                    block.Entity,
                    GroupMetadata(block, definition.Id),
                    title,
                    "TECH"));
            children.Add(titleId);
        }
    }

    private static void AddServiceEntranceInternalLinks(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        IReadOnlyDictionary<string, SymbolDefinition> symbols,
        ICollection<SceneElement> output,
        ICollection<SceneId> children)
    {
        BlockPartDefinition? meterPart =
            definition.Parts.SingleOrDefault(part =>
                string.Equals(
                    part.Id,
                    "METER",
                    StringComparison.Ordinal));
        BlockPartDefinition? protectionPart =
            definition.Parts.SingleOrDefault(part =>
                string.Equals(
                    part.Id,
                    "PROTECTION",
                    StringComparison.Ordinal));

        if (meterPart is null ||
            protectionPart is null ||
            !symbols.TryGetValue(
                meterPart.SymbolId,
                out SymbolDefinition? meter))
        {
            return;
        }

        string protectionSymbolId =
            block.SymbolOverrides.TryGetValue(
                protectionPart.Id,
                out string? overrideId)
                    ? overrideId
                    : protectionPart.SymbolId;

        if (!symbols.TryGetValue(
                protectionSymbolId,
                out SymbolDefinition? protection))
        {
            return;
        }

        AnchorDefinition? protectionIn =
            protection.Anchors.SingleOrDefault(anchor =>
                string.Equals(
                    anchor.Id,
                    "IN",
                    StringComparison.Ordinal));

        if (protectionIn is null)
        {
            return;
        }

        double axisX =
            positioned.Bounds.X +
            protectionPart.Offset.X +
            protectionIn.Point.X;
        double frameHeaderY =
            positioned.Bounds.Y + 12;
        double meterTop =
            positioned.Bounds.Y +
            meterPart.Offset.Y +
            meter.NominalBounds.Y;
        double meterBottom =
            meterTop +
            meter.NominalBounds.Height;
        double protectionTop =
            positioned.Bounds.Y +
            protectionPart.Offset.Y +
            protectionIn.Point.Y;

        AddServiceAxisSegment(
            block,
            definition,
            $"{block.Id}/service-axis/top",
            axisX,
            frameHeaderY,
            meterTop,
            output,
            children);
        AddServiceAxisSegment(
            block,
            definition,
            $"{block.Id}/service-axis/meter-protection",
            axisX,
            meterBottom,
            protectionTop,
            output,
            children);
    }

    private static void AddServiceAxisSegment(
        CompositionBlock block,
        BlockDefinition definition,
        string idValue,
        double x,
        double firstY,
        double secondY,
        ICollection<SceneElement> output,
        ICollection<SceneId> children)
    {
        if (firstY == secondY)
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
        SceneId id =
            new(idValue);

        output.Add(
            new LineSceneElement(
                id,
                BoundsFor([start, end]),
                SceneLayer.Power,
                15,
                SceneVisibility.Both,
                block.Entity,
                GroupMetadata(block, definition.Id),
                start,
                end,
                "POWER"));
        children.Add(id);
    }

    private static SceneAnchor[] NormalizeSummaryPowerAnchors(
        CompositionBlock block,
        MmRect bounds,
        IReadOnlyList<SceneAnchor> anchors)
    {
        if (block.SemanticRole is not ("Source" or "BoardSummary"))
        {
            return anchors.ToArray();
        }

        double centerX =
            bounds.X +
            (bounds.Width / 2.0);

        return anchors
            .Select(anchor =>
            {
                if (block.SemanticRole == "Source" &&
                    anchor.Role == AnchorRole.PowerOut)
                {
                    return new SceneAnchor(
                        anchor.Id,
                        anchor.Role,
                        new MmPoint(
                            centerX,
                            bounds.Bottom),
                        AnchorDirection.Down);
                }

                if (block.SemanticRole == "BoardSummary" &&
                    anchor.Role == AnchorRole.PowerIn)
                {
                    return new SceneAnchor(
                        anchor.Id,
                        anchor.Role,
                        new MmPoint(
                            centerX,
                            bounds.Y),
                        AnchorDirection.Up);
                }

                if (block.SemanticRole == "BoardSummary" &&
                    anchor.Role == AnchorRole.PowerOut)
                {
                    return new SceneAnchor(
                        anchor.Id,
                        anchor.Role,
                        new MmPoint(
                            centerX,
                            bounds.Bottom),
                        AnchorDirection.Down);
                }

                return anchor;
            })
            .OrderBy(
                anchor => anchor.Id,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssembleBlock(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        IReadOnlyDictionary<string, SymbolDefinition> symbols,
        IReadOnlyDictionary<string, LineStyleDefinition> lineStyles,
        MeasuredBlock? measuredBlock,
        ICollection<SceneElement> output)
    {
        if (positioned.Bounds.Width < definition.MinimumSize.Width ||
            positioned.Bounds.Height < definition.MinimumSize.Height)
        {
            throw new InvalidOperationException(
                $"Positioned bounds for '{block.Id}' are smaller than block definition '{definition.Id}'.");
        }

        var children = new List<SceneId>();
        var anchorCandidates = new List<(string PartId, SceneAnchor Anchor)>();

        foreach (BlockPartDefinition part in definition.Parts)
        {
            if (block.SemanticRole == "ServiceEntranceAssembly" &&
                string.Equals(
                    part.Id,
                    "FRAME",
                    StringComparison.Ordinal))
            {
                AssembleServiceEntranceFrame(
                    block,
                    positioned,
                    definition,
                    output,
                    children);
                continue;
            }

            string symbolId = block.SymbolOverrides.TryGetValue(
                part.Id,
                out string? overrideSymbolId)
                    ? overrideSymbolId
                    : part.SymbolId;

            if (!symbols.TryGetValue(symbolId, out SymbolDefinition? symbol))
            {
                throw new InvalidOperationException(
                    $"Drawing profile is missing symbol definition '{symbolId}'.");
            }

            MmPoint origin = new(
                positioned.Bounds.X + part.Offset.X,
                positioned.Bounds.Y + part.Offset.Y);
            MmRect symbolBounds = Translate(symbol.NominalBounds, origin);

            if (!Contains(positioned.Bounds, symbolBounds))
            {
                throw new InvalidOperationException(
                    $"Symbol '{symbol.Id}' does not fit inside positioned block '{block.Id}'.");
            }

            SceneId symbolSceneId = new(
                $"{block.Id}/part/{part.Id}/symbol");

            var symbolElement = new SymbolSceneElement(
                symbolSceneId,
                symbolBounds,
                SceneLayer.Symbol,
                20,
                SceneVisibility.Both,
                block.Entity,
                Metadata(
                    block,
                    definition.Id,
                    part.Id,
                    symbol.Id),
                symbol.Id);

            output.Add(symbolElement);
            children.Add(symbolSceneId);

            foreach (AnchorDefinition anchor in symbol.Anchors)
            {
                anchorCandidates.Add(
                    (
                        part.Id,
                        new SceneAnchor(
                            anchor.Id,
                            anchor.Role,
                            Translate(anchor.Point, origin),
                            anchor.Direction)
                    ));
            }

            for (int index = 0; index < symbol.Primitives.Count; index++)
            {
                SymbolPrimitive primitive = symbol.Primitives[index];
                SceneElement element = AssemblePrimitive(
                    block,
                    definition,
                    part,
                    symbol,
                    primitive,
                    index,
                    origin,
                    symbolBounds,
                    lineStyles);
                output.Add(element);
                children.Add(element.Id);
            }

            AddLabel(
                block,
                definition,
                part,
                symbol,
                origin,
                measuredBlock,
                output,
                children);
        }

        if (block.SemanticRole == "ServiceEntranceAssembly")
        {
            AddServiceEntranceInternalLinks(
                block,
                positioned,
                definition,
                symbols,
                output,
                children);
        }

        SceneAnchor[] groupAnchors =
            NormalizeDifferentialNeutralAnchors(
                block,
                AddSemanticDestinationAnchors(
                    block,
                    positioned.Bounds,
                    ResolveGroupAnchorIds(anchorCandidates)));
        groupAnchors =
            NormalizeSummaryPowerAnchors(
                block,
                positioned.Bounds,
                groupAnchors);

        var group = new GroupSceneElement(
            new SceneId(block.Id),
            positioned.Bounds,
            SceneLayer.Symbol,
            5,
            SceneVisibility.Both,
            block.Entity,
            GroupMetadata(block, definition.Id),
            children,
            groupAnchors);

        output.Add(group);
    }

    private static SceneElement AssemblePrimitive(
        CompositionBlock block,
        BlockDefinition definition,
        BlockPartDefinition part,
        SymbolDefinition symbol,
        SymbolPrimitive primitive,
        int index,
        MmPoint origin,
        MmRect symbolBounds,
        IReadOnlyDictionary<string, LineStyleDefinition> lineStyles)
    {
        if (!lineStyles.TryGetValue(
                primitive.LineStyleId,
                out LineStyleDefinition? lineStyle))
        {
            throw new InvalidOperationException(
                $"Drawing profile is missing line style '{primitive.LineStyleId}'.");
        }

        SceneId id = new(
            $"{block.Id}/part/{part.Id}/primitive/{index.ToString("D3", CultureInfo.InvariantCulture)}");
        SceneLayer layer = LayerFor(lineStyle.Role);
        IReadOnlyDictionary<string, string> metadata = Metadata(
            block,
            definition.Id,
            part.Id,
            symbol.Id);

        return primitive switch
        {
            LineSymbolPrimitive line => CreateLine(
                id,
                block,
                metadata,
                line,
                origin,
                layer),
            PolylineSymbolPrimitive polyline => CreatePolyline(
                id,
                block,
                metadata,
                polyline,
                origin,
                layer),
            RectangleSymbolPrimitive rectangle => new RectangleSceneElement(
                id,
                Translate(rectangle.Rectangle, origin),
                layer,
                15,
                SceneVisibility.Both,
                block.Entity,
                metadata,
                rectangle.LineStyleId),
            CircleSymbolPrimitive circle => CreateCircle(
                id,
                block,
                metadata,
                circle,
                origin,
                layer),
            ArcSymbolPrimitive arc => CreateArcPath(
                id,
                block,
                metadata,
                arc,
                origin,
                layer),
            PathSymbolPrimitive path => new PathSceneElement(
                id,
                symbolBounds,
                layer,
                15,
                SceneVisibility.Both,
                block.Entity,
                metadata,
                path.Data,
                path.LineStyleId),
            _ => throw new InvalidOperationException(
                $"Unsupported symbol primitive '{primitive.GetType().FullName}'.")
        };
    }

    private static LineSceneElement CreateLine(
        SceneId id,
        CompositionBlock block,
        IReadOnlyDictionary<string, string> metadata,
        LineSymbolPrimitive line,
        MmPoint origin,
        SceneLayer layer)
    {
        MmPoint start = Translate(line.Start, origin);
        MmPoint end = Translate(line.End, origin);

        return new LineSceneElement(
            id,
            BoundsFor([start, end]),
            layer,
            15,
            SceneVisibility.Both,
            block.Entity,
            metadata,
            start,
            end,
            line.LineStyleId);
    }

    private static PolylineSceneElement CreatePolyline(
        SceneId id,
        CompositionBlock block,
        IReadOnlyDictionary<string, string> metadata,
        PolylineSymbolPrimitive polyline,
        MmPoint origin,
        SceneLayer layer)
    {
        MmPoint[] points = polyline.Points
            .Select(point => Translate(point, origin))
            .ToArray();

        return new PolylineSceneElement(
            id,
            BoundsFor(points),
            layer,
            15,
            SceneVisibility.Both,
            block.Entity,
            metadata,
            points,
            polyline.LineStyleId);
    }

    private static CircleSceneElement CreateCircle(
        SceneId id,
        CompositionBlock block,
        IReadOnlyDictionary<string, string> metadata,
        CircleSymbolPrimitive circle,
        MmPoint origin,
        SceneLayer layer)
    {
        MmPoint center = Translate(circle.Center, origin);
        MmRect bounds = new(
            center.X - circle.Radius,
            center.Y - circle.Radius,
            circle.Radius * 2,
            circle.Radius * 2);

        return new CircleSceneElement(
            id,
            bounds,
            layer,
            15,
            SceneVisibility.Both,
            block.Entity,
            metadata,
            center,
            circle.Radius,
            circle.LineStyleId);
    }

    private static PathSceneElement CreateArcPath(
        SceneId id,
        CompositionBlock block,
        IReadOnlyDictionary<string, string> metadata,
        ArcSymbolPrimitive arc,
        MmPoint origin,
        SceneLayer layer)
    {
        MmPoint center = Translate(arc.Center, origin);
        double startRadians = arc.StartDegrees * Math.PI / 180.0;
        double endRadians =
            (arc.StartDegrees + arc.SweepDegrees) * Math.PI / 180.0;
        double startX = center.X + (arc.Radius * Math.Cos(startRadians));
        double startY = center.Y + (arc.Radius * Math.Sin(startRadians));
        double endX = center.X + (arc.Radius * Math.Cos(endRadians));
        double endY = center.Y + (arc.Radius * Math.Sin(endRadians));
        int largeArc = Math.Abs(arc.SweepDegrees) > 180 ? 1 : 0;
        int sweep = arc.SweepDegrees >= 0 ? 1 : 0;

        string data = FormattableString.Invariant(
            $"M {startX:R} {startY:R} A {arc.Radius:R} {arc.Radius:R} 0 {largeArc} {sweep} {endX:R} {endY:R}");
        MmRect bounds = new(
            center.X - arc.Radius,
            center.Y - arc.Radius,
            arc.Radius * 2,
            arc.Radius * 2);

        return new PathSceneElement(
            id,
            bounds,
            layer,
            15,
            SceneVisibility.Both,
            block.Entity,
            metadata,
            data,
            arc.LineStyleId);
    }

    private static void AddLabel(
        CompositionBlock block,
        BlockDefinition definition,
        BlockPartDefinition part,
        SymbolDefinition symbol,
        MmPoint origin,
        MeasuredBlock? measuredBlock,
        ICollection<SceneElement> output,
        ICollection<SceneId> children)
    {
        if (part.LabelSlotId is null ||
            !block.Labels.TryGetValue(part.LabelSlotId, out string? text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        LabelSlot slot = symbol.LabelSlots
            .SingleOrDefault(label =>
                string.Equals(
                    label.Id,
                    part.LabelSlotId,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Symbol '{symbol.Id}' does not contain label slot '{part.LabelSlotId}'.");

        SceneId id = new(
            $"{block.Id}/part/{part.Id}/label/{slot.Id}");
        MmRect textBounds =
            ResolveTextBounds(
                slot,
                origin,
                measuredBlock);

        var element = new TextSceneElement(
            id,
            textBounds,
            SceneLayer.Text,
            30,
            SceneVisibility.Both,
            block.Entity,
            Metadata(block, definition.Id, part.Id, symbol.Id),
            text,
            slot.TextStyleId);

        output.Add(element);
        children.Add(id);
    }

    private static MmRect ResolveTextBounds(
        LabelSlot slot,
        MmPoint origin,
        MeasuredBlock? measuredBlock)
    {
        if (measuredBlock is null)
        {
            return Translate(
                slot.Bounds,
                origin);
        }

        if (!measuredBlock.Labels.TryGetValue(
                slot.Id,
                out TextMeasurement measurement))
        {
            throw new InvalidOperationException(
                $"Measured block '{measuredBlock.BlockId}' does not contain label measurement '{slot.Id}'.");
        }

        return new MmRect(
            origin.X + slot.Bounds.X,
            origin.Y + slot.Bounds.Y,
            Math.Max(
                measurement.WidthMm,
                MinimumPrimitiveExtentMm),
            measurement.HeightMm);
    }

    private static SceneAnchor[] NormalizeDifferentialNeutralAnchors(
        CompositionBlock block,
        IReadOnlyList<SceneAnchor> anchors)
    {
        if (block.SemanticRole != "DifferentialProtection")
        {
            return anchors.ToArray();
        }

        SceneAnchor? powerAxis = anchors
            .FirstOrDefault(anchor =>
                anchor.Role is
                    AnchorRole.PowerIn or
                    AnchorRole.PowerOut);

        if (powerAxis is null)
        {
            return anchors.ToArray();
        }

        return anchors
            .Select(anchor =>
                anchor.Role == AnchorRole.Neutral
                    ? new SceneAnchor(
                        anchor.Id,
                        anchor.Role,
                        new MmPoint(
                            powerAxis.Point.X +
                            Math.Abs(
                                powerAxis.Point.X -
                                anchor.Point.X),
                            anchor.Point.Y),
                        AnchorDirection.Right)
                    : anchor)
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static SceneAnchor[] AddSemanticDestinationAnchors(
        CompositionBlock block,
        MmRect bounds,
        IReadOnlyList<SceneAnchor> anchors)
    {
        if (block.SemanticRole is not ("FinalLoad" or "DownstreamBoard" or "Unknown"))
        {
            return anchors.ToArray();
        }

        var result =
            new List<SceneAnchor>(anchors);

        if (!result.Any(anchor =>
                string.Equals(
                    anchor.Id,
                    "N",
                    StringComparison.Ordinal)))
        {
            result.Add(
                new SceneAnchor(
                    "N",
                    AnchorRole.Neutral,
                    new MmPoint(
                        bounds.X + (bounds.Width * 0.68),
                        bounds.Y + 2),
                    AnchorDirection.Up));
        }

        if (!result.Any(anchor =>
                string.Equals(
                    anchor.Id,
                    "PE",
                    StringComparison.Ordinal)))
        {
            result.Add(
                new SceneAnchor(
                    "PE",
                    AnchorRole.Ground,
                    new MmPoint(
                        bounds.X + (bounds.Width * 0.32),
                        bounds.Y + 2),
                    AnchorDirection.Up));
        }

        return result
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static SceneAnchor[] ResolveGroupAnchorIds(
        IReadOnlyList<(string PartId, SceneAnchor Anchor)> candidates)
    {
        Dictionary<string, int> counts = candidates
            .GroupBy(item => item.Anchor.Id, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);

        return candidates
            .Select(item =>
                counts[item.Anchor.Id] == 1
                    ? item.Anchor
                    : new SceneAnchor(
                        $"{item.PartId}:{item.Anchor.Id}",
                        item.Anchor.Role,
                        item.Anchor.Point,
                        item.Anchor.Direction))
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static SceneConnection AssembleConnection(
        CompositionConnection connection,
        IReadOnlyDictionary<string, GroupSceneElement> groups,
        IReadOnlyDictionary<string, LineStyleDefinition> lineStyles)
    {
        if (!groups.TryGetValue(
                connection.Source.BlockId,
                out GroupSceneElement? source))
        {
            throw new InvalidOperationException(
                $"Connection source block '{connection.Source.BlockId}' was not assembled.");
        }

        if (!groups.TryGetValue(
                connection.Target.BlockId,
                out GroupSceneElement? target))
        {
            throw new InvalidOperationException(
                $"Connection target block '{connection.Target.BlockId}' was not assembled.");
        }

        if (!lineStyles.TryGetValue(
                connection.LineStyleId,
                out LineStyleDefinition? style))
        {
            throw new InvalidOperationException(
                $"Drawing profile is missing line style '{connection.LineStyleId}'.");
        }

        string sourceAnchorId = ResolveAnchorId(
            source,
            connection.Source);
        string targetAnchorId = ResolveAnchorId(
            target,
            connection.Target);

        return new SceneConnection(
            new SceneId(connection.Id),
            new SceneAnchorRef(source.Id, sourceAnchorId),
            new SceneAnchorRef(target.Id, targetAnchorId),
            connection.LineStyleId,
            LayerFor(style.Role),
            10,
            SceneVisibility.Both,
            connection.SemanticEntity);
    }

    private static string ResolveAnchorId(
        GroupSceneElement group,
        CompositionAnchorRef reference)
    {
        if (reference.PreferredAnchorId is not null &&
            group.Anchors.Any(anchor =>
                string.Equals(
                    anchor.Id,
                    reference.PreferredAnchorId,
                    StringComparison.Ordinal)))
        {
            return reference.PreferredAnchorId;
        }

        SceneAnchor? byRole = group.Anchors
            .Where(anchor => anchor.Role == reference.Role)
            .OrderBy(anchor => anchor.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (byRole is not null)
        {
            return byRole.Id;
        }

        throw new InvalidOperationException(
            $"Block '{reference.BlockId}' has no anchor compatible with role '{reference.Role}'.");
    }

    private static SceneLayer LayerFor(LineSemanticRole role) =>
        role switch
        {
            LineSemanticRole.Power => SceneLayer.Power,
            LineSemanticRole.Bus => SceneLayer.Power,
            LineSemanticRole.Ground => SceneLayer.Grounding,
            LineSemanticRole.Reference => SceneLayer.Annotation,
            LineSemanticRole.Annotation => SceneLayer.Annotation,
            LineSemanticRole.Boundary => SceneLayer.Annotation,
            LineSemanticRole.AlternateSupply => SceneLayer.Power,
            _ => SceneLayer.Annotation
        };

    private static IReadOnlyDictionary<string, string> Metadata(
        CompositionBlock block,
        string blockDefinitionId,
        string partId,
        string symbolId) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["blockDefinitionId"] = blockDefinitionId,
            ["compositionRole"] = block.SemanticRole,
            ["partId"] = partId,
            ["status"] = block.Status.ToString(),
            ["symbolId"] = symbolId
        };

    private static IReadOnlyDictionary<string, string> GroupMetadata(
        CompositionBlock block,
        string blockDefinitionId)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["blockDefinitionId"] = blockDefinitionId,
            ["compositionRole"] = block.SemanticRole,
            ["status"] = block.Status.ToString()
        };

        if (block.ParentId is not null)
        {
            metadata["parentId"] = block.ParentId;
        }

        return metadata;
    }

    private static MmPoint Translate(MmPoint point, MmPoint origin) =>
        new(
            origin.X + point.X,
            origin.Y + point.Y);

    private static MmRect Translate(MmRect rect, MmPoint origin) =>
        new(
            origin.X + rect.X,
            origin.Y + rect.Y,
            rect.Width,
            rect.Height);

    private static MmRect BoundsFor(IReadOnlyList<MmPoint> points)
    {
        double minX = points.Min(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxX = points.Max(point => point.X);
        double maxY = points.Max(point => point.Y);

        return new MmRect(
            minX,
            minY,
            Math.Max(maxX - minX, MinimumPrimitiveExtentMm),
            Math.Max(maxY - minY, MinimumPrimitiveExtentMm));
    }

    private static bool Contains(MmRect outer, MmRect inner) =>
        inner.X >= outer.X &&
        inner.Y >= outer.Y &&
        inner.Right <= outer.Right &&
        inner.Bottom <= outer.Bottom;
}
