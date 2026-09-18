namespace UI_Unilineal.Rendering.Avalonia.Tests.Architecture;

public sealed class RenderingDependencyBoundaryTests
{
    [Fact]
    public void RenderingAvalonia_ProjectReferencesDomainAndEngineButNotPlaygroundOrHost()
    {
        string repositoryRoot = FindRepositoryRoot();
        string projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "UI_Unilineal.Rendering.Avalonia",
            "UI_Unilineal.Rendering.Avalonia.csproj");
        string content = File.ReadAllText(projectPath);

        Assert.Contains(
            @"..\UI_Unilineal.Domain\UI_Unilineal.Domain.csproj",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            @"..\UI_Unilineal.Engine\UI_Unilineal.Engine.csproj",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "UI_Unilineal.Playground",
            content,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ProyectoElectrico",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RenderingAvalonia_RuntimeReferencesDoNotIncludePlaygroundOrHost()
    {
        string[] references =
            typeof(UI_Unilineal.Rendering.Avalonia.AssemblyMarker)
                .Assembly
                .GetReferencedAssemblies()
                .Select(x => x.Name ?? string.Empty)
                .ToArray();

        Assert.DoesNotContain(
            references,
            x => x.StartsWith(
                "UI_Unilineal.Playground",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            x => x.StartsWith(
                "ProyectoElectrico",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DomainAndEngine_StillDoNotReferenceAvalonia()
    {
        Assert.DoesNotContain(
            typeof(UI_Unilineal.Domain.AssemblyMarker)
                .Assembly
                .GetReferencedAssemblies(),
            x => (x.Name ?? string.Empty).StartsWith(
                "Avalonia",
                StringComparison.Ordinal));

        Assert.DoesNotContain(
            typeof(UI_Unilineal.Engine.AssemblyMarker)
                .Assembly
                .GetReferencedAssemblies(),
            x => (x.Name ?? string.Empty).StartsWith(
                "Avalonia",
                StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "UI_Unilineal.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate UI_Unilineal repository root.");
    }
}
