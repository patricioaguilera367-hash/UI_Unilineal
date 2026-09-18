namespace UI_Unilineal.Engine.Tests.Architecture;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Domain_DoesNotReferenceUiOrHostAssemblies()
    {
        string[] references = typeof(Domain.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, x => x.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.StartsWith("ProyectoElectrico", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.Contains("Infrastructure.Csv", StringComparison.Ordinal));
    }

    [Fact]
    public void Engine_DoesNotReferenceUiOrHostAssemblies()
    {
        string[] references = typeof(Engine.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("UI_Unilineal.Domain", references);
        Assert.DoesNotContain(references, x => x.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.StartsWith("ProyectoElectrico", StringComparison.Ordinal));
    }

    [Fact]
    public void DomainAndEngine_SourceDoesNotReferenceRendererOrHost()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] sourceDirectories =
        [
            Path.Combine(repositoryRoot, "src", "UI_Unilineal.Domain"),
            Path.Combine(repositoryRoot, "src", "UI_Unilineal.Engine")
        ];

        foreach (string sourceDirectory in sourceDirectories)
        {
            IEnumerable<string> files = Directory
                .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(path =>
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Where(path =>
                    !path.Contains(
                        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase) &&
                    !path.Contains(
                        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.OrdinalIgnoreCase));

            foreach (string file in files)
            {
                string content = File.ReadAllText(file);

                Assert.DoesNotContain(
                    "Avalonia",
                    content,
                    StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "ProyectoElectrico",
                    content,
                    StringComparison.Ordinal);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "UI_Unilineal.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate UI_Unilineal repository root.");
    }
}
