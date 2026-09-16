namespace UI_Unilineal.Domain.Tests;

public sealed class ArchitectureSmokeTests
{
    [Fact]
    public void DomainAssembly_IsLoadable()
    {
        var assembly = typeof(Domain.AssemblyMarker).Assembly;

        Assert.Equal(
            "UI_Unilineal.Domain",
            assembly.GetName().Name);
    }
}
