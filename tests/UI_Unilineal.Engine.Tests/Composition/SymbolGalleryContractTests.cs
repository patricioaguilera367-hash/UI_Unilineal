using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class SymbolGalleryContractTests
{
    [Fact]
    public void RepositoryCatalog_AllSymbolsSatisfyGalleryContract()
    {
        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader().LoadDirectory(
                Path.Combine(AppContext.BaseDirectory, "ProfileData"));

        HashSet<string> lineStyles = profile.LineStyles
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> textStyles = profile.TextStyles
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> provenance = profile.Provenance
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(profile.Symbols);

        foreach (SymbolDefinition symbol in profile.Symbols)
        {
            Assert.True(double.IsFinite(symbol.NominalBounds.X));
            Assert.True(double.IsFinite(symbol.NominalBounds.Y));
            Assert.True(symbol.NominalBounds.Width > 0);
            Assert.True(symbol.NominalBounds.Height > 0);

            Assert.All(
                symbol.Anchors,
                anchor => Assert.True(symbol.NominalBounds.Contains(anchor.Point)));
            Assert.All(
                symbol.Primitives,
                primitive => Assert.Contains(primitive.LineStyleId, lineStyles));
            Assert.All(
                symbol.LabelSlots,
                label => Assert.Contains(label.TextStyleId, textStyles));
            Assert.NotEmpty(symbol.ProvenanceIds);
            Assert.All(
                symbol.ProvenanceIds,
                id => Assert.Contains(id, provenance));
        }
    }
}
