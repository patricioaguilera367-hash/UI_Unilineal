namespace UI_Unilineal.Engine.Interaction.Electrical;

public interface IElectricalCommandHandler
{
    ValueTask<CommandResult> ExecuteAsync(
        ElectricalCommandRequest request,
        CancellationToken cancellationToken = default);
}
