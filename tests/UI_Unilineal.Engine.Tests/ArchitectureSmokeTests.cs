namespace UI_Unilineal.Engine.Tests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void EngineAssembly_IsLoadable()
    {
        var assembly = typeof(Engine.AssemblyMarker).Assembly;

        Assert.Equal(
            "UI_Unilineal.Engine",
            assembly.GetName().Name);
    }

    [Fact]
    public void Engine_CanReferenceDomain()
    {
        var domainAssembly =
            typeof(Domain.AssemblyMarker).Assembly;

        Assert.Equal(
            "UI_Unilineal.Domain",
            domainAssembly.GetName().Name);
    }
}
