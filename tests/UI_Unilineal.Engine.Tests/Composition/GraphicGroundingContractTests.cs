using System.Text.Json;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class GraphicGroundingContractTests
{
    [Fact]
    public void GraphicConventions_CoverEveryCurrentSymbolExactlyOnce()
    {
        RIC18DrawingProfile profile = LoadProfile();
        using JsonDocument document = LoadJson("graphic-conventions.json");

        JsonElement families = document.RootElement.GetProperty("families");
        var members = families
            .EnumerateArray()
            .SelectMany(family => family.GetProperty("members").EnumerateArray())
            .Select(member => member.GetString()!)
            .ToArray();

        Assert.Equal(
            profile.Symbols.Select(symbol => symbol.Id).OrderBy(id => id, StringComparer.Ordinal),
            members.OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(
            members.Length,
            members.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GraphicConventions_InitialCellsMatchCurrentNominalBounds()
    {
        RIC18DrawingProfile profile = LoadProfile();
        using JsonDocument document = LoadJson("graphic-conventions.json");

        var cells = document.RootElement
            .GetProperty("families")
            .EnumerateArray()
            .SelectMany(family =>
            {
                JsonElement cell = family.GetProperty("nominalCell");
                double width = cell.GetProperty("widthMm").GetDouble();
                double height = cell.GetProperty("heightMm").GetDouble();

                return family.GetProperty("members")
                    .EnumerateArray()
                    .Select(member => new
                    {
                        Id = member.GetString()!,
                        Width = width,
                        Height = height
                    })
                    .ToArray();
            })
            .ToDictionary(item => item.Id, StringComparer.Ordinal);

        foreach (var symbol in profile.Symbols)
        {
            Assert.True(cells.TryGetValue(symbol.Id, out var cell));
            Assert.Equal(symbol.NominalBounds.Width, cell!.Width, 6);
            Assert.Equal(symbol.NominalBounds.Height, cell.Height, 6);
        }
    }

    [Fact]
    public void Annex18_5Mapping_CoversEveryCurrentSymbolWithoutPrematurePromotion()
    {
        RIC18DrawingProfile profile = LoadProfile();
        using JsonDocument document = LoadJson("annex-18.5-symbol-mapping.json");

        JsonElement[] entries = document.RootElement.EnumerateArray().ToArray();
        string[] mappedIds = entries
            .Select(entry => entry.GetProperty("symbolId").GetString()!)
            .ToArray();

        Assert.Equal(
            profile.Symbols.Select(symbol => symbol.Id).OrderBy(id => id, StringComparer.Ordinal),
            mappedIds.OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(
            mappedIds.Length,
            mappedIds.Distinct(StringComparer.Ordinal).Count());

        string[] allowedReferenceStatuses =
            ["VERIFIED", "PARTIAL", "NOT_SHOWN", "AMBIGUOUS"];
        string[] allowedGeometryStatuses =
            ["REFERENCE_ALIGNED", "PARTIAL_MATCH", "MISMATCH", "APP_ONLY"];

        Assert.All(entries, entry =>
        {
            Assert.Contains(
                entry.GetProperty("referenceStatus").GetString(),
                allowedReferenceStatuses);
            Assert.Contains(
                entry.GetProperty("currentGeometryStatus").GetString(),
                allowedGeometryStatuses);
            string shapeSourceId =
                entry.GetProperty("shapeSourceId").GetString()!;
            string sizeSourceId =
                entry.GetProperty("sizeSourceId").GetString()!;

            Assert.Contains(
                profile.Provenance,
                source => source.Id == shapeSourceId);
            Assert.Contains(
                profile.Provenance,
                source => source.Id == sizeSourceId);

            string symbolId =
                entry.GetProperty("symbolId").GetString()!;
            var symbol = profile.Symbols.Single(
                candidate => candidate.Id == symbolId);

            Assert.Contains(shapeSourceId, symbol.ProvenanceIds);
            Assert.Contains(sizeSourceId, symbol.ProvenanceIds);
        });
    }


    [Fact]
    public void BreakerVariants_UseSharedCellAnchorsAndOnePoleMarkerPerProtectedConductor()
    {
        RIC18DrawingProfile profile = LoadProfile();

        for (int multiplicity = 1; multiplicity <= 4; multiplicity++)
        {
            SymbolDefinition symbol = profile.Symbols.Single(
                candidate => candidate.Id == $"BREAKER_{multiplicity}X");

            Assert.Equal(12, symbol.NominalBounds.Width, 6);
            Assert.Equal(16, symbol.NominalBounds.Height, 6);

            AnchorDefinition input = symbol.Anchors.Single(anchor => anchor.Id == "IN");
            AnchorDefinition output = symbol.Anchors.Single(anchor => anchor.Id == "OUT");

            Assert.Equal(6, input.Point.X, 6);
            Assert.Equal(0, input.Point.Y, 6);
            Assert.Equal(6, output.Point.X, 6);
            Assert.Equal(16, output.Point.Y, 6);

            int horizontalPoleMarkers = symbol.Primitives
                .OfType<LineSymbolPrimitive>()
                .Count(line =>
                    Math.Abs(line.Start.Y - line.End.Y) < 0.000001);

            Assert.Equal(multiplicity, horizontalPoleMarkers);
            Assert.Equal(
                2,
                symbol.Primitives.OfType<CircleSymbolPrimitive>().Count());
            Assert.Contains(
                "RIC18:ANNEX18.5:DIAGRAMA_UNILINEAL",
                symbol.ProvenanceIds);
            Assert.Contains(
                "APP:GRAPHIC_CONVENTIONS",
                symbol.ProvenanceIds);
        }
    }

    [Fact]
    public void Annex18_5Reference_IsRegisteredAsReferenceWithLocator()
    {
        RIC18DrawingProfile profile = LoadProfile();

        GraphicRuleSource source = Assert.Single(
            profile.Provenance,
            item => item.Id == "RIC18:ANNEX18.5:DIAGRAMA_UNILINEAL");

        Assert.Equal(
            GraphicRuleClassification.RIC18_REFERENCE,
            source.Classification);
        Assert.Equal("18.5", source.Annex);
        Assert.False(string.IsNullOrWhiteSpace(source.Document));
    }

    private static RIC18DrawingProfile LoadProfile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(ProfileDataPath());

    private static JsonDocument LoadJson(string fileName) =>
        JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(ProfileDataPath(), fileName)));

    private static string ProfileDataPath() =>
        Path.Combine(AppContext.BaseDirectory, "ProfileData");
}
