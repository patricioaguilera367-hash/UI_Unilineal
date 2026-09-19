using UI_Unilineal.Domain.Grounding;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class GroundingSchemeCatalogTests
{
    [Fact]
    public void RepositoryCatalog_IsManualAndDefaultsToTnS()
    {
        GroundingSchemeCatalog catalog =
            LoadCatalog();

        Assert.Equal(
            "MANUAL",
            catalog.SelectionMode);
        Assert.Equal(
            "TN_S",
            catalog.DefaultSchemeId);

        GroundingSchemeDefinition selected =
            Assert.Single(
                catalog.Schemes,
                scheme => scheme.DefaultSelected);

        Assert.Equal(
            "TN_S",
            selected.Id);
        Assert.Equal(
            "TP_TS_SIDE_BY_SIDE_SHARED_UPSTREAM_BRANCH",
            selected.PreviewPresetId);
    }

    [Fact]
    public void DefaultTnSPreview_PlacesTpAndTsSideBySideOnSharedUpstreamBranch()
    {
        GroundingSchemeCatalog catalog =
            LoadCatalog();

        GroundingSchemeDefinition selected =
            catalog.Schemes.Single(
                scheme => scheme.Id == catalog.DefaultSchemeId);
        GroundingPreviewPresetDefinition preset =
            Assert.Single(
                catalog.PreviewPresets,
                candidate => candidate.Id == selected.PreviewPresetId);

        Assert.Equal(
            "VISUAL_DEFAULT_ONLY_PENDING_TOPOLOGY",
            preset.Status);
        Assert.Equal(
            ["GROUND_TP", "GROUND_TS"],
            preset.Symbols
                .Select(symbol => symbol.SymbolId)
                .ToArray());
        Assert.Equal(
            3,
            preset.Segments.Count);

        GroundingPreviewSymbolPlacement tp =
            preset.Symbols.Single(
                symbol => symbol.SymbolId == "GROUND_TP");
        GroundingPreviewSymbolPlacement ts =
            preset.Symbols.Single(
                symbol => symbol.SymbolId == "GROUND_TS");

        Assert.Equal(0, tp.X, 6);
        Assert.Equal(6, tp.Y, 6);
        Assert.Equal(18, ts.X, 6);
        Assert.Equal(6, ts.Y, 6);
    }

    [Fact]
    public void RepositoryCatalog_PredefinesRequiredSchemeTitlesWithPendingConnections()
    {
        GroundingSchemeCatalog catalog =
            LoadCatalog();

        Assert.Equal(
            ["IT", "TN_C", "TN_C_S", "TN_S", "TT"],
            catalog.Schemes
                .Select(scheme => scheme.Id)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());

        Assert.All(
            catalog.Schemes,
            scheme =>
            {
                Assert.Equal(
                    "PENDING_DEFINITION",
                    scheme.ConnectionStatus);
                Assert.Empty(
                    scheme.ConnectionPoints);
                Assert.Equal(
                    ["GROUND_TP", "GROUND_TS"],
                    scheme.SymbolIds);
                Assert.Equal(
                    "Pliego Técnico Normativo RIC N°05",
                    scheme.SourceDocument);
            });
    }

    [Fact]
    public void CanonicalGroundSymbols_HaveDistinctReferenceGeometry()
    {
        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader()
                .LoadDirectory(ProfileDataPath());

        SymbolDefinition tp =
            profile.Symbols.Single(
                symbol => symbol.Id == "GROUND_TP");
        SymbolDefinition ts =
            profile.Symbols.Single(
                symbol => symbol.Id == "GROUND_TS");

        Assert.Equal(
            "ProtectiveGround",
            tp.SemanticRole);
        Assert.Equal(
            "ServiceGround",
            ts.SemanticRole);

        Assert.Equal(
            14,
            tp.NominalBounds.Width,
            6);
        Assert.Equal(
            14,
            ts.NominalBounds.Width,
            6);

        Assert.Equal(
            9,
            tp.Primitives.OfType<LineSymbolPrimitive>().Count());
        Assert.Equal(
            4,
            ts.Primitives.OfType<LineSymbolPrimitive>().Count());

        Assert.Contains(
            "RIC18:ANNEX18.5:DIAGRAMA_UNILINEAL",
            tp.ProvenanceIds);
        Assert.Contains(
            "RIC18:ANNEX18.5:DIAGRAMA_UNILINEAL",
            ts.ProvenanceIds);
    }

    private static GroundingSchemeCatalog LoadCatalog() =>
        new GroundingSchemeCatalogLoader()
            .LoadDirectory(ProfileDataPath());

    private static string ProfileDataPath() =>
        Path.Combine(
            AppContext.BaseDirectory,
            "ProfileData");
}
