namespace UI_Unilineal.Engine.Interaction;

public sealed record HostCapabilities(
    bool CanEditLayout,
    bool CanEditElectrical,
    bool CanCreateCircuits,
    bool CanDeleteCircuits,
    bool CanEditProtection,
    bool CanExport,
    bool CanPersistLayout)
{
    public static HostCapabilities ReadOnly { get; } =
        new(
            CanEditLayout: false,
            CanEditElectrical: false,
            CanCreateCircuits: false,
            CanDeleteCircuits: false,
            CanEditProtection: false,
            CanExport: false,
            CanPersistLayout: false);
}
