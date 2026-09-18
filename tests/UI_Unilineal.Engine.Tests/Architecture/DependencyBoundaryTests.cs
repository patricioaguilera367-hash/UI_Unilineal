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
}
