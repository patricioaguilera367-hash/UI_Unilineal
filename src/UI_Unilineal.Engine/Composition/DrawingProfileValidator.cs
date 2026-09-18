using System.Text.RegularExpressions;
using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Engine.Composition;

public sealed partial class DrawingProfileValidator
{
    public DrawingProfileValidationResult Validate(
        RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var issues = new List<ProfileValidationIssue>();

        ValidateVersion(profile, issues);
        AddDuplicateIssues(profile, issues);

        IReadOnlyDictionary<string, GraphicRuleSource> provenance =
            FirstById(profile.Provenance, item => item.Id);
        IReadOnlyDictionary<string, LineStyleDefinition> lineStyles =
            FirstById(profile.LineStyles, item => item.Id);
        IReadOnlyDictionary<string, TextStyleDefinition> textStyles =
            FirstById(profile.TextStyles, item => item.Id);
        IReadOnlyDictionary<string, SymbolDefinition> symbols =
            FirstById(profile.Symbols, item => item.Id);

        ValidateProvenance(profile.Provenance, issues);
        ValidateStyles(profile, provenance, issues);
        ValidateLayout(profile.Layout, provenance, issues);
        ValidateSymbols(
            profile.Symbols,
            provenance,
            lineStyles,
            textStyles,
            issues);
        ValidateBlocks(profile.Blocks, provenance, symbols, issues);

        ProfileValidationIssue[] sorted = issues
            .OrderBy(issue => issue.Severity)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.EntityId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Field, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal)
            .ToArray();

        return new DrawingProfileValidationResult(sorted);
    }

    private static void ValidateVersion(
        RIC18DrawingProfile profile,
        ICollection<ProfileValidationIssue> issues)
    {
        if (!SemanticVersionRegex().IsMatch(profile.Version))
        {
            issues.Add(Error(
                ProfileValidationCodes.InvalidVersion,
                $"Profile version '{profile.Version}' is not semantic version metadata.",
                profile.ProfileId,
                nameof(RIC18DrawingProfile.Version)));
        }
    }

    private static void AddDuplicateIssues(
        RIC18DrawingProfile profile,
        ICollection<ProfileValidationIssue> issues)
    {
        AddDuplicates(
            profile.Provenance,
            item => item.Id,
            ProfileValidationCodes.DuplicateProvenanceId,
            issues);
        AddDuplicates(
            profile.Symbols,
            item => item.Id,
            ProfileValidationCodes.DuplicateSymbolId,
            issues);
        AddDuplicates(
            profile.Blocks,
            item => item.Id,
            ProfileValidationCodes.DuplicateBlockId,
            issues);
        AddDuplicates(
            profile.LineStyles,
            item => item.Id,
            ProfileValidationCodes.DuplicateLineStyleId,
            issues);
        AddDuplicates(
            profile.TextStyles,
            item => item.Id,
            ProfileValidationCodes.DuplicateTextStyleId,
            issues);
    }

    private static void ValidateProvenance(
        IEnumerable<GraphicRuleSource> provenance,
        ICollection<ProfileValidationIssue> issues)
    {
        foreach (GraphicRuleSource source in provenance)
        {
            if (source.Classification == GraphicRuleClassification.APP_CONVENTION)
            {
                continue;
            }

            bool hasDocument = !string.IsNullOrWhiteSpace(source.Document);
            bool hasLocator =
                !string.IsNullOrWhiteSpace(source.Section) ||
                !string.IsNullOrWhiteSpace(source.Annex) ||
                !string.IsNullOrWhiteSpace(source.Figure);

            if (!hasDocument || !hasLocator)
            {
                issues.Add(Error(
                    ProfileValidationCodes.RicProvenanceMissingCitation,
                    $"RIC-classified provenance '{source.Id}' requires a document and an explicit locator.",
                    source.Id));
            }
        }
    }

    private static void ValidateStyles(
        RIC18DrawingProfile profile,
        IReadOnlyDictionary<string, GraphicRuleSource> provenance,
        ICollection<ProfileValidationIssue> issues)
    {
        foreach (LineStyleDefinition style in profile.LineStyles)
        {
            RequireProvenance(
                style.ProvenanceId,
                provenance,
                style.Id,
                nameof(LineStyleDefinition.ProvenanceId),
                issues);
        }

        foreach (TextStyleDefinition style in profile.TextStyles)
        {
            RequireProvenance(
                style.ProvenanceId,
                provenance,
                style.Id,
                nameof(TextStyleDefinition.ProvenanceId),
                issues);
        }
    }

    private static void ValidateLayout(
        LayoutProfile layout,
        IReadOnlyDictionary<string, GraphicRuleSource> provenance,
        ICollection<ProfileValidationIssue> issues) =>
        RequireProvenance(
            layout.ProvenanceId,
            provenance,
            "LAYOUT",
            nameof(LayoutProfile.ProvenanceId),
            issues);

    private static void ValidateSymbols(
        IEnumerable<SymbolDefinition> symbols,
        IReadOnlyDictionary<string, GraphicRuleSource> provenance,
        IReadOnlyDictionary<string, LineStyleDefinition> lineStyles,
        IReadOnlyDictionary<string, TextStyleDefinition> textStyles,
        ICollection<ProfileValidationIssue> issues)
    {
        foreach (SymbolDefinition symbol in symbols)
        {
            if (symbol.ProvenanceIds.Count == 0)
            {
                issues.Add(Error(
                    ProfileValidationCodes.MissingProvenance,
                    $"Symbol '{symbol.Id}' has no provenance.",
                    symbol.Id,
                    nameof(SymbolDefinition.ProvenanceIds)));
            }

            foreach (string provenanceId in symbol.ProvenanceIds)
            {
                RequireProvenance(
                    provenanceId,
                    provenance,
                    symbol.Id,
                    nameof(SymbolDefinition.ProvenanceIds),
                    issues);
            }

            foreach (var anchor in symbol.Anchors)
            {
                if (!symbol.NominalBounds.Contains(anchor.Point))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.AnchorOutsideBounds,
                        $"Anchor '{anchor.Id}' is outside symbol bounds.",
                        symbol.Id,
                        anchor.Id));
                }
            }

            foreach (SymbolPrimitive primitive in symbol.Primitives)
            {
                if (!lineStyles.ContainsKey(primitive.LineStyleId))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.MissingLineStyle,
                        $"Line style '{primitive.LineStyleId}' does not exist.",
                        symbol.Id,
                        nameof(SymbolPrimitive.LineStyleId)));
                }

                if (!PrimitiveGeometryIsValid(primitive))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.InvalidPrimitiveGeometry,
                        "Symbol primitive geometry is invalid.",
                        symbol.Id));
                }
            }

            foreach (LabelSlot label in symbol.LabelSlots)
            {
                if (!textStyles.ContainsKey(label.TextStyleId))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.MissingTextStyle,
                        $"Text style '{label.TextStyleId}' does not exist.",
                        symbol.Id,
                        label.Id));
                }

                if (!Contains(symbol.NominalBounds, label.Bounds))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.InvalidPrimitiveGeometry,
                        $"Label slot '{label.Id}' is outside symbol bounds.",
                        symbol.Id,
                        label.Id));
                }
            }
        }
    }

    private static void ValidateBlocks(
        IEnumerable<BlockDefinition> blocks,
        IReadOnlyDictionary<string, GraphicRuleSource> provenance,
        IReadOnlyDictionary<string, SymbolDefinition> symbols,
        ICollection<ProfileValidationIssue> issues)
    {
        foreach (BlockDefinition block in blocks)
        {
            if (block.ProvenanceIds.Count == 0)
            {
                issues.Add(Error(
                    ProfileValidationCodes.MissingProvenance,
                    $"Block '{block.Id}' has no provenance.",
                    block.Id,
                    nameof(BlockDefinition.ProvenanceIds)));
            }

            foreach (string provenanceId in block.ProvenanceIds)
            {
                RequireProvenance(
                    provenanceId,
                    provenance,
                    block.Id,
                    nameof(BlockDefinition.ProvenanceIds),
                    issues);
            }

            foreach (BlockPartDefinition part in block.Parts)
            {
                if (!symbols.TryGetValue(part.SymbolId, out SymbolDefinition? symbol))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.MissingSymbol,
                        $"Symbol '{part.SymbolId}' does not exist.",
                        block.Id,
                        part.Id));
                    continue;
                }

                if (part.LabelSlotId is not null &&
                    !symbol.LabelSlots.Any(slot =>
                        string.Equals(
                            slot.Id,
                            part.LabelSlotId,
                            StringComparison.Ordinal)))
                {
                    issues.Add(Error(
                        ProfileValidationCodes.MissingLabelSlot,
                        $"Label slot '{part.LabelSlotId}' does not exist on symbol '{part.SymbolId}'.",
                        block.Id,
                        part.Id));
                }
            }
        }
    }

    private static bool PrimitiveGeometryIsValid(SymbolPrimitive primitive) =>
        primitive switch
        {
            CircleSymbolPrimitive circle =>
                double.IsFinite(circle.Radius) && circle.Radius > 0,
            ArcSymbolPrimitive arc =>
                double.IsFinite(arc.Radius) &&
                arc.Radius > 0 &&
                double.IsFinite(arc.StartDegrees) &&
                double.IsFinite(arc.SweepDegrees),
            PathSymbolPrimitive path =>
                !string.IsNullOrWhiteSpace(path.Data),
            PolylineSymbolPrimitive polyline =>
                polyline.Points.Count >= 2,
            _ => true
        };

    private static bool Contains(MmRect outer, MmRect inner) =>
        inner.X >= outer.X &&
        inner.Y >= outer.Y &&
        inner.Right <= outer.Right &&
        inner.Bottom <= outer.Bottom;

    private static void RequireProvenance(
        string provenanceId,
        IReadOnlyDictionary<string, GraphicRuleSource> provenance,
        string entityId,
        string field,
        ICollection<ProfileValidationIssue> issues)
    {
        if (provenance.ContainsKey(provenanceId))
        {
            return;
        }

        issues.Add(Error(
            ProfileValidationCodes.MissingProvenance,
            $"Provenance '{provenanceId}' does not exist.",
            entityId,
            field));
    }

    private static void AddDuplicates<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector,
        string code,
        ICollection<ProfileValidationIssue> issues)
    {
        foreach (IGrouping<string, T> group in values
                     .GroupBy(idSelector, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(Error(
                code,
                $"Duplicate ID '{group.Key}'.",
                group.Key));
        }
    }

    private static IReadOnlyDictionary<string, T> FirstById<T>(
        IEnumerable<T> values,
        Func<T, string> idSelector) =>
        values
            .GroupBy(idSelector, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);

    private static ProfileValidationIssue Error(
        string code,
        string message,
        string? entityId = null,
        string? field = null) =>
        new(
            code,
            ProfileValidationSeverity.Error,
            message,
            entityId,
            field);

    [GeneratedRegex(@"^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersionRegex();
}
