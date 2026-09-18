using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class CompositionGoldenTests
{
    [Theory]
    [InlineData("minimal-detail.txt", "minimal", "B1")]
    [InlineData("nested-detail.txt", "nested", "B1")]
    public void BoardDetail_MatchesReviewedStructuralGolden(
        string fileName,
        string fixture,
        string boardUid)
    {
        SingleLineInput input = fixture == "minimal"
            ? SemanticFixtureFactory.Minimal()
            : SemanticFixtureFactory.NestedBoards();
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(input);
        Assert.True(projectionResult.Success);
        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(projectionResult.Projection);
        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader().LoadDirectory(
                Path.Combine(AppContext.BaseDirectory, "ProfileData"));

        DrawingComposition composition =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                new EntityUid(boardUid),
                profile);

        string actual = Normalize(CompositionGoldenFormatter.Format(composition));
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Golden",
            "Composition",
            fileName);

        Assert.True(
            File.Exists(path),
            $"Golden file does not exist: {path}{Environment.NewLine}ACTUAL:{Environment.NewLine}{actual}");

        string expected = Normalize(File.ReadAllText(path));
        Assert.Equal(expected, actual);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
