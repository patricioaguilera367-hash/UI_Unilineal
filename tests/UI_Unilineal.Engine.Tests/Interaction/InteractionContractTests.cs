using System.Reflection;
using UI_Unilineal.Engine.Interaction;

namespace UI_Unilineal.Engine.Tests.Interaction;

public sealed class InteractionContractTests
{
    [Fact]
    public void InteractionMode_DefinesExactlyNavigateLayoutAndElectrical()
    {
        Assert.Equal(
            [
                InteractionMode.Navigate,
                InteractionMode.Layout,
                InteractionMode.Electrical
            ],
            Enum.GetValues<InteractionMode>());
    }

    [Fact]
    public void InteractionStateKind_DefinesTheApprovedExplicitStates()
    {
        Assert.Equal(
            [
                InteractionStateKind.Idle,
                InteractionStateKind.Hovering,
                InteractionStateKind.Selecting,
                InteractionStateKind.Panning,
                InteractionStateKind.DraggingLayout,
                InteractionStateKind.ConnectingElectrical,
                InteractionStateKind.MarqueeSelecting,
                InteractionStateKind.CommandPreview
            ],
            Enum.GetValues<InteractionStateKind>());
    }

    [Fact]
    public void CommandImpact_DefinesTheApprovedImpactClasses()
    {
        Assert.Equal(
            [
                CommandImpact.PresentationOnly,
                CommandImpact.LocalElectrical,
                CommandImpact.CascadingElectrical,
                CommandImpact.Destructive
            ],
            Enum.GetValues<CommandImpact>());
    }

    [Fact]
    public void HostCapabilities_ReadOnly_DisablesEveryMutationCapability()
    {
        HostCapabilities capabilities =
            HostCapabilities.ReadOnly;

        Assert.False(capabilities.CanEditLayout);
        Assert.False(capabilities.CanEditElectrical);
        Assert.False(capabilities.CanCreateCircuits);
        Assert.False(capabilities.CanDeleteCircuits);
        Assert.False(capabilities.CanEditProtection);
        Assert.False(capabilities.CanExport);
        Assert.False(capabilities.CanPersistLayout);
    }

    [Fact]
    public void InteractionContracts_DoNotReferenceUiOrHostAssemblies()
    {
        Assembly engine =
            typeof(InteractionMode).Assembly;

        string[] references =
            engine.GetReferencedAssemblies()
                .Select(reference =>
                    reference.Name ?? string.Empty)
                .ToArray();

        Assert.DoesNotContain(
            references,
            name =>
                name.StartsWith(
                    "Avalonia",
                    StringComparison.Ordinal));
        Assert.DoesNotContain(
            references,
            name =>
                name.StartsWith(
                    "ProyectoElectrico",
                    StringComparison.Ordinal));
    }
}
