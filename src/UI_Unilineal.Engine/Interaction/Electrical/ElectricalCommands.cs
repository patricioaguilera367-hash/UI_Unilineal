using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Electrical;

public sealed record ChangeBoardSupplyCommand(
    EntityUid BoardUid,
    EntityUid SupplyConnectionUid) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.CascadingElectrical;
}

public sealed record ChangeSupplyCircuitCommand(
    EntityUid SupplyConnectionUid,
    EntityUid? CircuitUid) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.CascadingElectrical;
}

public sealed record ChangeProtectionCommand(
    EntityUid ProtectionUid,
    ProtectionInput Replacement) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.CascadingElectrical;
}

public sealed record ChangeConductorCommand(
    EntityUid CircuitUid,
    ConductorInput? Conductor) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.CascadingElectrical;
}

public sealed record ChangeCircuitDataCommand(
    EntityUid CircuitUid,
    CircuitInput Replacement) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.CascadingElectrical;
}

public sealed record CreateSupplyConnectionCommand(
    SupplyConnection Connection) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.CascadingElectrical;
}

public sealed record RemoveSupplyConnectionCommand(
    EntityUid SupplyConnectionUid) : IElectricalCommand
{
    public CommandImpact Impact =>
        CommandImpact.Destructive;
}
