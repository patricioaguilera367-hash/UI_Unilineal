using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Projection;

public sealed class ProjectionGoldenTests
{
    [Theory]
    [InlineData("minimal-project.txt", "minimal")]
    [InlineData("nested-boards.txt", "nested")]
    public void Projection_MatchesReviewedStructuralGolden(string fileName, string fixture)
    {
        ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(
            fixture == "minimal"
                ? SemanticFixtureFactory.Minimal()
                : SemanticFixtureFactory.NestedBoards());

        Assert.True(result.Success);
        Assert.NotNull(result.Projection);

        string actual = Normalize(ProjectionGoldenFormatter.Format(result.Projection!));
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "Golden",
            "Projection",
            fileName);

        Assert.True(File.Exists(path), $"Golden file does not exist: {path}");

        string expected = Normalize(File.ReadAllText(path));

        Assert.Equal(expected, actual);
    }

    private static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);
}
