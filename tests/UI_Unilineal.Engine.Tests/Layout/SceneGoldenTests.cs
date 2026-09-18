using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class SceneGoldenTests
{
    [Theory]
    [InlineData("minimal-summary.txt", "minimal", "summary")]
    [InlineData("nested-summary.txt", "nested", "summary")]
    [InlineData("minimal-detail.txt", "minimal", "detail")]
    [InlineData("nested-detail.txt", "nested", "detail")]
    public void Layout_MatchesReviewedStructuralGolden(
        string fileName,
        string fixture,
        string view)
    {
        SingleLineInput input = fixture == "minimal"
            ? SemanticFixtureFactory.Minimal()
            : SemanticFixtureFactory.NestedBoards();
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(input);
        Assert.True(projectionResult.Success);
        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(
                projectionResult.Projection);
        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader().LoadDirectory(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "ProfileData"));
        var engine = new SingleLineLayoutEngine(new DeterministicTextMetrics());

        SingleLineLayoutResult result = view == "summary"
            ? engine.LayoutSummary(projection, profile)
            : engine.LayoutBoardDetail(
                projection,
                new EntityUid("B1"),
                profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);

        string actual = Normalize(
            SceneGoldenFormatter.Format(
                Assert.IsType<Domain.Scene.DiagramScene>(
                    result.Scene)));
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Golden",
            "Scene",
            fileName);

        Assert.True(
            File.Exists(path),
            $"Golden file does not exist: {path}{Environment.NewLine}" +
            $"ACTUAL:{Environment.NewLine}{actual}");

        string expected = Normalize(
            File.ReadAllText(path));

        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            Assert.Fail(
                $"Scene golden mismatch: {fileName}{Environment.NewLine}" +
                $"ACTUAL:{Environment.NewLine}{actual}");
        }
    }

    private static string Normalize(string value) =>
        value.Replace(
            "\r\n",
            "\n",
            StringComparison.Ordinal);
}
