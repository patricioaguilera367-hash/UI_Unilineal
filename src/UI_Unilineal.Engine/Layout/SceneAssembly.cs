using System.Globalization;
using UI_Unilineal.Domain.Blocks;
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

            if (block.SemanticRole is "NeutralBus" or "ProtectiveEarthBus")
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

    private static void AssembleStructuralRail(
        CompositionBlock block,
        PositionedCompositionBlock positioned,
        BlockDefinition definition,
        ICollection<SceneElement> output)
    {
        string lineStyleId =
            block.SemanticRole == "NeutralBus"
                ? "BUS"
                : "GROUND";
        AnchorRole anchorRole =
            block.SemanticRole == "NeutralBus"
                ? AnchorRole.Neutral
                : AnchorRole.Ground;
        SceneLayer layer =
            block.SemanticRole == "NeutralBus"
                ? SceneLayer.Power
                : SceneLayer.Grounding;

        double left = positioned.Bounds.X + 4;
        double right = positioned.Bounds.Right - 4;
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
            new List<SceneAnchor>
            {
                new(
                    "IN",
                    anchorRole,
                    new MmPoint(left, y),
                    AnchorDirection.Left),
                new(
                    "OUT",
                    anchorRole,
                    new MmPoint(right, y),
                    AnchorDirection.Right)
            };

        for (int index = 0; index < taps.Length; index++)
        {
            double fraction =
                (index + 1.0) /
                (taps.Length + 1.0);
            double x =
                left +
                ((right - left) * fraction);

            anchors.Add(
                new SceneAnchor(
                    $"TAP:{taps[index]}",
                    anchorRole,
                    new MmPoint(x, y),
                    AnchorDirection.Down));
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
                [railId],
                anchors));
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

        SceneAnchor[] groupAnchors =
            ResolveGroupAnchorIds(anchorCandidates);

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
