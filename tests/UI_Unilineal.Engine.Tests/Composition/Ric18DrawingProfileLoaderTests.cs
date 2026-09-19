using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class Ric18DrawingProfileLoaderTests
{
    [Fact]
    public void LoadDirectory_EmptyDirectory_ThrowsFileNotFoundException()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"UI_Unilineal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            Assert.Throws<FileNotFoundException>(
                () => new Ric18DrawingProfileLoader().LoadDirectory(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_RepositoryV1Profile_PassesValidation()
    {
        RIC18DrawingProfile profile = LoadRepositoryProfile();

        DrawingProfileValidationResult validation =
            new DrawingProfileValidator().Validate(profile);

        Assert.False(
            validation.HasErrors,
            string.Join(Environment.NewLine, validation.Issues));
    }

    [Fact]
    public void LoadDirectory_RepositoryV1Profile_ContainsInitialCatalog()
    {
        RIC18DrawingProfile profile = LoadRepositoryProfile();

        string[] requiredSymbols =
        [
            "SOURCE_UTILITY",
            "SERVICE_ENTRANCE",
            "BREAKER",
            "BREAKER_1X",
            "BREAKER_2X",
            "BREAKER_3X",
            "BREAKER_4X",
            "RCD",
            "FUSE",
            "BUS",
            "GROUND",
            "CONNECTION_NODE",
            "DOWNSTREAM_BOARD",
            "FINAL_LOAD",
            "UNKNOWN_ENDPOINT"
        ];

        string[] requiredBlocks =
        [
            "SOURCE_BLOCK",
            "BOARD_SUMMARY_BLOCK",
            "INCOMING_SUPPLY_BLOCK",
            "MAIN_PROTECTION_BLOCK",
            "MAIN_BUS_BLOCK",
            "CIRCUIT_BRANCH_BLOCK",
            "PROTECTION_CHAIN_BLOCK",
            "DOWNSTREAM_BOARD_BLOCK",
            "FINAL_LOAD_BLOCK",
            "GROUNDING_BLOCK",
            "UNKNOWN_BLOCK"
        ];

        Assert.All(
            requiredSymbols,
            id => Assert.Contains(profile.Symbols, symbol => symbol.Id == id));
        Assert.All(
            requiredBlocks,
            id => Assert.Contains(profile.Blocks, block => block.Id == id));

        Assert.Contains(
            profile.Provenance,
            source =>
                source.Id == "RIC18:ANNEX18.5:DIAGRAMA_UNILINEAL" &&
                source.Classification == GraphicRuleClassification.RIC18_REFERENCE);

        Assert.All(
            profile.Provenance.Where(source =>
                source.Id.StartsWith("APP:", StringComparison.Ordinal)),
            source => Assert.Equal(
                GraphicRuleClassification.APP_CONVENTION,
                source.Classification));
    }

    private static RIC18DrawingProfile LoadRepositoryProfile()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "ProfileData");
        return new Ric18DrawingProfileLoader().LoadDirectory(path);
    }
}
