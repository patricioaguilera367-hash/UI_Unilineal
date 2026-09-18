using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class DrawingProfileValidatorTests
{
    [Fact]
    public void Validate_ValidApplicationConventionProfile_HasNoErrors()
    {
        RIC18DrawingProfile profile = ValidProfile();

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(profile);

        Assert.False(result.HasErrors);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_DuplicateCatalogIds_ReturnsErrors()
    {
        RIC18DrawingProfile source = ValidProfile();
        RIC18DrawingProfile profile = Profile(
            provenance: [source.Provenance[0], source.Provenance[0]],
            symbols: [source.Symbols[0], source.Symbols[0]],
            blocks: [source.Blocks[0], source.Blocks[0]],
            lineStyles: [source.LineStyles[0], source.LineStyles[0]],
            textStyles: [source.TextStyles[0], source.TextStyles[0]]);

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(profile);

        AssertCodes(
            result,
            ProfileValidationCodes.DuplicateProvenanceId,
            ProfileValidationCodes.DuplicateSymbolId,
            ProfileValidationCodes.DuplicateBlockId,
            ProfileValidationCodes.DuplicateLineStyleId,
            ProfileValidationCodes.DuplicateTextStyleId);
    }

    [Fact]
    public void Validate_MissingStyleReferences_ReturnsErrors()
    {
        RIC18DrawingProfile source = ValidProfile();
        SymbolDefinition invalidSymbol = new(
            "BREAKER",
            "Breaker",
            new MmRect(0, 0, 10, 10),
            source.Symbols[0].Anchors,
            [
                new LineSymbolPrimitive(
                    new MmPoint(0, 5),
                    new MmPoint(10, 5),
                    "MISSING_LINE")
            ],
            [
                new LabelSlot(
                    "RATING",
                    new MmRect(0, 0, 10, 3),
                    0,
                    true,
                    "MISSING_TEXT")
            ],
            ["APP:BASE"]);

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(Profile(symbols: [invalidSymbol]));

        AssertCodes(
            result,
            ProfileValidationCodes.MissingLineStyle,
            ProfileValidationCodes.MissingTextStyle);
    }

    [Fact]
    public void Validate_AnchorOutsideNominalBounds_ReturnsError()
    {
        SymbolDefinition symbol = new(
            "BREAKER",
            "Breaker",
            new MmRect(0, 0, 10, 10),
            [
                new AnchorDefinition(
                    "OUT",
                    AnchorRole.PowerOut,
                    new MmPoint(11, 5),
                    AnchorDirection.Right)
            ],
            [
                new LineSymbolPrimitive(
                    new MmPoint(0, 5),
                    new MmPoint(10, 5),
                    "POWER")
            ],
            [],
            ["APP:BASE"]);

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(Profile(symbols: [symbol]));

        Assert.Contains(
            result.Issues,
            x => x.Code == ProfileValidationCodes.AnchorOutsideBounds);
    }

    [Fact]
    public void Validate_BlockWithMissingSymbolAndProvenance_ReturnsErrors()
    {
        var block = new BlockDefinition(
            "BLOCK",
            "MainProtection",
            new MmSize(20, 20),
            [
                new BlockPartDefinition(
                    "P1",
                    "MISSING_SYMBOL",
                    new MmPoint(0, 0),
                    null)
            ],
            ["MISSING_PROVENANCE"]);

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(Profile(blocks: [block]));

        AssertCodes(
            result,
            ProfileValidationCodes.MissingSymbol,
            ProfileValidationCodes.MissingProvenance);
    }

    [Fact]
    public void Validate_RicClassificationWithoutCitation_ReturnsError()
    {
        var provenance = new GraphicRuleSource(
            "RIC:UNVERIFIED",
            GraphicRuleClassification.RIC18_EXPLICIT,
            null,
            null,
            null,
            null,
            "A rule incorrectly promoted without a citation.");

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(Profile(provenance: [provenance]));

        Assert.Contains(
            result.Issues,
            x => x.Code == ProfileValidationCodes.RicProvenanceMissingCitation);
    }

    [Fact]
    public void EveryGraphicRuleHasProvenance()
    {
        RIC18DrawingProfile profile = ValidProfile();

        DrawingProfileValidationResult result =
            new DrawingProfileValidator().Validate(profile);

        Assert.DoesNotContain(
            result.Issues,
            x => x.Code == ProfileValidationCodes.MissingProvenance);
    }

    [Fact]
    public void NoAppConventionClaimsRicExplicit()
    {
        RIC18DrawingProfile profile = ValidProfile();

        Assert.All(
            profile.Provenance.Where(x => x.Id.StartsWith("APP:", StringComparison.Ordinal)),
            source => Assert.Equal(
                GraphicRuleClassification.APP_CONVENTION,
                source.Classification));
    }

    private static RIC18DrawingProfile ValidProfile() =>
        Profile();

    private static RIC18DrawingProfile Profile(
        IReadOnlyList<GraphicRuleSource>? provenance = null,
        IReadOnlyList<SymbolDefinition>? symbols = null,
        IReadOnlyList<BlockDefinition>? blocks = null,
        IReadOnlyList<LineStyleDefinition>? lineStyles = null,
        IReadOnlyList<TextStyleDefinition>? textStyles = null)
    {
        GraphicRuleSource appSource = new(
            "APP:BASE",
            GraphicRuleClassification.APP_CONVENTION,
            null,
            null,
            null,
            null,
            "Initial application drawing convention; no regulatory geometry claim.");

        LineStyleDefinition lineStyle = new(
            "POWER",
            LineSemanticRole.Power,
            0.35,
            LinePattern.Solid,
            "APP:BASE");

        TextStyleDefinition textStyle = new(
            "TECH",
            "Arial",
            2.5,
            false,
            "APP:BASE");

        SymbolDefinition symbol = new(
            "BREAKER",
            "Breaker",
            new MmRect(0, 0, 10, 10),
            [
                new AnchorDefinition(
                    "IN",
                    AnchorRole.PowerIn,
                    new MmPoint(0, 5),
                    AnchorDirection.Left),
                new AnchorDefinition(
                    "OUT",
                    AnchorRole.PowerOut,
                    new MmPoint(10, 5),
                    AnchorDirection.Right)
            ],
            [
                new LineSymbolPrimitive(
                    new MmPoint(0, 5),
                    new MmPoint(10, 5),
                    "POWER")
            ],
            [
                new LabelSlot(
                    "RATING",
                    new MmRect(0, 0, 10, 3),
                    0,
                    true,
                    "TECH")
            ],
            ["APP:BASE"]);

        BlockDefinition block = new(
            "MAIN_PROTECTION_BLOCK",
            "MainProtection",
            new MmSize(20, 20),
            [
                new BlockPartDefinition(
                    "BREAKER",
                    "BREAKER",
                    new MmPoint(5, 5),
                    "RATING")
            ],
            ["APP:BASE"]);

        return new RIC18DrawingProfile(
            "RIC18-V1",
            "1.0.0",
            null,
            provenance ?? [appSource],
            symbols ?? [symbol],
            blocks ?? [block],
            lineStyles ?? [lineStyle],
            textStyles ?? [textStyle],
            new LayoutProfile(
                2.5,
                12,
                12,
                8,
                3,
                1,
                260,
                20,
                "APP:BASE"));
    }

    private static void AssertCodes(
        DrawingProfileValidationResult result,
        params string[] codes)
    {
        foreach (string code in codes)
        {
            Assert.Contains(result.Issues, x => x.Code == code);
        }
    }
}
