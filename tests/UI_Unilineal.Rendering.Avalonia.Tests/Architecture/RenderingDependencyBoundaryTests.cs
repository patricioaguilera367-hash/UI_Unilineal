namespace UI_Unilineal.Rendering.Avalonia.Tests.Architecture;

public sealed class RenderingDependencyBoundaryTests
{
    [Fact]
    public void RenderingAvalonia_ReferencesDomainAndEngineButNotPlaygroundOrHost()
    {
        string[] references = typeof(UI_Unilineal.Rendering.Avalonia.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("UI_Unilineal.Domain", references);
        Assert.Contains("UI_Unilineal.Engine", references);
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
}
