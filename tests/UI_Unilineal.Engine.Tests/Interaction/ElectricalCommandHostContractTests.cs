using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction.Electrical;

namespace UI_Unilineal.Engine.Tests.Interaction;

public sealed class ElectricalCommandHostContractTests
{
    [Fact]
    public void ResultStatus_DefinesTheApprovedHostOutcomes()
    {
        Assert.Equal(
            [
                CommandResultStatus.Applied,
                CommandResultStatus.Rejected,
                CommandResultStatus.NeedsConfirmation,
                CommandResultStatus.Conflict,
                CommandResultStatus.Failed
            ],
            Enum.GetValues<CommandResultStatus>());
    }

    [Fact]
    public void Request_RequiresExpectedRevision()
    {
        IElectricalCommand command =
            new RemoveSupplyConnectionCommand(
                new EntityUid("supply-1"));

        Assert.Throws<ArgumentException>(
            () =>
                new ElectricalCommandRequest(
                    command,
                    " "));
    }

    [Fact]
    public void AppliedResult_CarriesNewSnapshotRevisionAndInverseCommand()
    {
        SingleLineInput input =
            Input();
        IElectricalCommand inverse =
            new CreateSupplyConnectionCommand(
                Supply());

        CommandResult result =
            CommandResult.Applied(
                input,
                "REV-002",
                inverse,
                [
                    new EntityReference(
                        new EntityUid("board-1"),
                        EntityKind.Board)
                ],
                [
                    new EntityReference(
                        new EntityUid("circuit-1"),
                        EntityKind.Circuit)
                ],
                ["Recalculation required."]);

        Assert.Equal(
            CommandResultStatus.Applied,
            result.Status);
        Assert.Same(
            input,
            result.NewInput);
        Assert.Equal(
            "REV-002",
            result.NewRevision);
        Assert.Same(
            inverse,
            result.InverseCommand);
        Assert.Single(result.ChangedEntities);
        Assert.Single(result.InvalidatedEntities);
        Assert.Single(result.Diagnostics);
    }

    [Theory]
    [InlineData(CommandResultStatus.Rejected)]
    [InlineData(CommandResultStatus.NeedsConfirmation)]
    [InlineData(CommandResultStatus.Conflict)]
    [InlineData(CommandResultStatus.Failed)]
    public void NonAppliedResult_CannotExposeFalselyAppliedState(
        CommandResultStatus status)
    {
        CommandResult result =
            status switch
            {
                CommandResultStatus.Rejected =>
                    CommandResult.Rejected(
                        ["Rejected."]),
                CommandResultStatus.NeedsConfirmation =>
                    CommandResult.NeedsConfirmation(
                        ["Confirmation required."]),
                CommandResultStatus.Conflict =>
                    CommandResult.Conflict(
                        ["Revision conflict."]),
                CommandResultStatus.Failed =>
                    CommandResult.Failed(
                        ["Execution failed."]),
                _ => throw new InvalidOperationException()
            };

        Assert.Equal(status, result.Status);
        Assert.Null(result.NewInput);
        Assert.Null(result.NewRevision);
        Assert.Null(result.InverseCommand);
        Assert.Empty(result.ChangedEntities);
        Assert.Empty(result.InvalidatedEntities);
    }

    [Fact]
    public void ElectricalUndo_IsRepresentableAsInverseRequestAtCurrentRevision()
    {
        IElectricalCommand inverse =
            new CreateSupplyConnectionCommand(
                Supply());
        CommandResult applied =
            CommandResult.Applied(
                Input(),
                "REV-002",
                inverse);

        var undoRequest =
            new ElectricalCommandRequest(
                applied.InverseCommand!,
                applied.NewRevision!,
                confirmationGranted: true);

        Assert.Same(
            inverse,
            undoRequest.Command);
        Assert.Equal(
            "REV-002",
            undoRequest.ExpectedRevision);
        Assert.True(
            undoRequest.ConfirmationGranted);
    }

    [Fact]
    public async Task HandlerContract_ExecutesTypedRequest()
    {
        IElectricalCommandHandler handler =
            new RejectingHandler();
        var request =
            new ElectricalCommandRequest(
                new RemoveSupplyConnectionCommand(
                    new EntityUid("supply-1")),
                "REV-001",
                confirmationGranted: true);

        CommandResult result =
            await handler.ExecuteAsync(
                request);

        Assert.Equal(
            CommandResultStatus.Rejected,
            result.Status);
    }

    private sealed class RejectingHandler :
        IElectricalCommandHandler
    {
        public ValueTask<CommandResult> ExecuteAsync(
            ElectricalCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return ValueTask.FromResult(
                CommandResult.Rejected(
                    ["Test handler."]));
        }
    }

    private static SingleLineInput Input() =>
        new(
            new ProjectInput(
                new EntityUid("project-1"),
                "P1",
                "Project",
                OperationalState.Active),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            new SingleLineInputMetadata(
                "1",
                "TEST",
                "host-contract"));

    private static SupplyConnection Supply() =>
        new(
            new EntityUid("supply-1"),
            new EntityReference(
                new EntityUid("source-1"),
                EntityKind.Source),
            null,
            new EntityUid("board-1"),
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);
}
