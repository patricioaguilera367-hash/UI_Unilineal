using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Interaction.Electrical;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Playground.Interaction;

public sealed class PlaygroundElectricalCommandHandler :
    IElectricalCommandHandler
{
    private SingleLineInput _input;
    private string _revision;

    public PlaygroundElectricalCommandHandler(
        SingleLineInput input)
    {
        _input =
            input ??
            throw new ArgumentNullException(
                nameof(input));
        _revision =
            SingleLineInputFingerprint.Compute(
                input);
    }

    public ValueTask<CommandResult> ExecuteAsync(
        ElectricalCommandRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(
                request.ExpectedRevision,
                _revision,
                StringComparison.Ordinal))
        {
            return ValueTask.FromResult(
                CommandResult.Conflict(
                    ["The Playground input revision changed."]));
        }

        if (request.Command.Impact is
                CommandImpact.CascadingElectrical or
                CommandImpact.Destructive &&
            !request.ConfirmationGranted)
        {
            return ValueTask.FromResult(
                CommandResult.NeedsConfirmation(
                    ["This demo command requires explicit confirmation."]));
        }

        return request.Command switch
        {
            CreateSupplyConnectionCommand create =>
                ValueTask.FromResult(
                    ApplyCreate(create)),
            RemoveSupplyConnectionCommand remove =>
                ValueTask.FromResult(
                    ApplyRemove(remove)),
            _ =>
                ValueTask.FromResult(
                    CommandResult.Rejected(
                        [$"The Playground demo host does not implement '{request.Command.GetType().Name}'."]))
        };
    }

    private CommandResult ApplyCreate(
        CreateSupplyConnectionCommand command)
    {
        SupplyConnection connection =
            command.Connection;

        if (_input.SupplyConnections.Any(
                existing =>
                    existing.Uid == connection.Uid))
        {
            return CommandResult.Rejected(
                [$"Supply connection '{connection.Uid.Value}' already exists."]);
        }

        SingleLineInput candidate =
            WithSupplyConnections(
                _input.SupplyConnections.Append(
                    connection));

        ProjectionBuildResult projection =
            new SingleLineProjectionBuilder().Build(
                candidate);

        if (!projection.Success)
        {
            return CommandResult.Rejected(
                projection.Validation.Issues
                    .Select(issue =>
                        $"{issue.Code}: {issue.Message}"));
        }

        Commit(candidate);

        var entity =
            new EntityReference(
                connection.Uid,
                EntityKind.SupplyConnection);
        var destination =
            new EntityReference(
                connection.DestinationBoardUid,
                EntityKind.Board);

        return CommandResult.Applied(
            candidate,
            _revision,
            new RemoveSupplyConnectionCommand(
                connection.Uid),
            changedEntities: [entity],
            invalidatedEntities: [destination]);
    }

    private CommandResult ApplyRemove(
        RemoveSupplyConnectionCommand command)
    {
        SupplyConnection? existing =
            _input.SupplyConnections.SingleOrDefault(
                connection =>
                    connection.Uid ==
                    command.SupplyConnectionUid);

        if (existing is null)
        {
            return CommandResult.Rejected(
                [$"Supply connection '{command.SupplyConnectionUid.Value}' does not exist."]);
        }

        SingleLineInput candidate =
            WithSupplyConnections(
                _input.SupplyConnections.Where(
                    connection =>
                        connection.Uid !=
                        command.SupplyConnectionUid));

        ProjectionBuildResult projection =
            new SingleLineProjectionBuilder().Build(
                candidate);

        if (!projection.Success)
        {
            return CommandResult.Rejected(
                projection.Validation.Issues
                    .Select(issue =>
                        $"{issue.Code}: {issue.Message}"));
        }

        Commit(candidate);

        var entity =
            new EntityReference(
                existing.Uid,
                EntityKind.SupplyConnection);
        var destination =
            new EntityReference(
                existing.DestinationBoardUid,
                EntityKind.Board);

        return CommandResult.Applied(
            candidate,
            _revision,
            new CreateSupplyConnectionCommand(
                existing),
            changedEntities: [entity],
            invalidatedEntities: [destination]);
    }

    private void Commit(
        SingleLineInput candidate)
    {
        _input = candidate;
        _revision =
            SingleLineInputFingerprint.Compute(
                candidate);
    }

    private SingleLineInput WithSupplyConnections(
        IEnumerable<SupplyConnection> connections) =>
        new(
            _input.Project,
            _input.Sources,
            _input.Boards,
            _input.Buses,
            _input.Circuits,
            connections,
            _input.Protections,
            _input.Grounding,
            _input.Results,
            _input.Metadata);
}
