using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class DrawingProfileFingerprintTests
{
    [Fact]
    public void Compute_ShuffledTopLevelCollections_IsStable()
    {
        RIC18DrawingProfile source = Load();
        var shuffled = new RIC18DrawingProfile(
            source.ProfileId,
            source.Version,
            source.SourceDocument,
            source.Provenance.Reverse(),
            source.Symbols.Reverse(),
            source.Blocks.Reverse(),
            source.LineStyles.Reverse(),
            source.TextStyles.Reverse(),
            source.Layout);

        Assert.Equal(
            DrawingProfileFingerprint.Compute(source),
            DrawingProfileFingerprint.Compute(shuffled));
    }

    [Fact]
    public void Compute_ChangingPrimitiveGeometry_ChangesFingerprint()
    {
        RIC18DrawingProfile source = Load();
        SymbolDefinition original = source.Symbols.Single(x => x.Id == "BREAKER");
        LineSymbolPrimitive firstLine = Assert.IsType<LineSymbolPrimitive>(original.Primitives[0]);
        var changedLine = firstLine with
        {
            End = new UI_Unilineal.Domain.Scene.MmPoint(
                firstLine.End.X + 0.5,
                firstLine.End.Y)
        };

        var changedSymbol = new SymbolDefinition(
            original.Id,
            original.SemanticRole,
            original.NominalBounds,
            original.Anchors,
            [changedLine, .. original.Primitives.Skip(1)],
            original.LabelSlots,
            original.ProvenanceIds);

        var changed = new RIC18DrawingProfile(
            source.ProfileId,
            source.Version,
            source.SourceDocument,
            source.Provenance,
            source.Symbols.Select(x => x.Id == original.Id ? changedSymbol : x),
            source.Blocks,
            source.LineStyles,
            source.TextStyles,
            source.Layout);

        Assert.NotEqual(
            DrawingProfileFingerprint.Compute(source),
            DrawingProfileFingerprint.Compute(changed));
    }

    [Fact]
    public void Compute_ChangingLayoutValue_ChangesFingerprint()
    {
        RIC18DrawingProfile source = Load();
        LayoutProfile layout = source.Layout with
        {
            HorizontalGapMm = source.Layout.HorizontalGapMm + 1
        };
        var changed = new RIC18DrawingProfile(
            source.ProfileId,
            source.Version,
            source.SourceDocument,
            source.Provenance,
            source.Symbols,
            source.Blocks,
            source.LineStyles,
            source.TextStyles,
            layout);

        Assert.NotEqual(
            DrawingProfileFingerprint.Compute(source),
            DrawingProfileFingerprint.Compute(changed));
    }

    private static RIC18DrawingProfile Load() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
