using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Interaction.Electrical;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Interaction;

public sealed class ElectricalCommandContractTests
{
    [Fact]
    public void ConnectionProposal_IsDeterministicAndRequiresConfirmation()
    {
        SupplyConnection connection =
            Supply();
        var command =
            new CreateSupplyConnectionCommand(
                connection);
        ElectricalAnchorEndpoint source =
            Endpoint(
                "source-1",
                EntityKind.Source,
                "power-out",
                AnchorRole.PowerOut);
        ElectricalAnchorEndpoint target =
            Endpoint(
                "board-1",
                EntityKind.Board,
                "power-in",
                AnchorRole.PowerIn);

        ElectricalCommandProposal first =
            ElectricalCommandProposalFactory.Create(
                command,
                "REV-001",
                source,
                target);
        ElectricalCommandProposal second =
            ElectricalCommandProposalFactory.Create(
                command,
                "REV-001",
                source,
                target);

        Assert.Equal(
            CommandImpact.CascadingElectrical,
            first.Impact);
        Assert.True(first.RequiresConfirmation);
        Assert.Equal(
            first.ProposalFingerprint,
            second.ProposalFingerprint);
        Assert.Equal(64, first.ProposalFingerprint.Length);
        Assert.Equal(
            first.ProposalFingerprint.ToUpperInvariant(),
            first.ProposalFingerprint);
    }

    [Fact]
    public void DestructiveCommand_AlwaysRequiresConfirmation()
    {
        var command =
            new RemoveSupplyConnectionCommand(
                new EntityUid("supply-1"));

        ElectricalCommandProposal proposal =
            ElectricalCommandProposalFactory.Create(
                command,
                "REV-001");

        Assert.Equal(
            CommandImpact.Destructive,
            proposal.Impact);
        Assert.True(proposal.RequiresConfirmation);
    }

    [Fact]
    public void IncompatibleAnchors_AreRejected()
    {
        var command =
            new CreateSupplyConnectionCommand(
                Supply());

        ElectricalAnchorEndpoint source =
            Endpoint(
                "source-1",
                EntityKind.Source,
                "power-in-a",
                AnchorRole.PowerIn);
        ElectricalAnchorEndpoint target =
            Endpoint(
                "board-1",
                EntityKind.Board,
                "power-in-b",
                AnchorRole.PowerIn);

        Assert.Throws<ArgumentException>(
            () =>
                ElectricalCommandProposalFactory.Create(
                    command,
                    "REV-001",
                    source,
                    target));
    }

    [Fact]
    public void ConnectionCommand_RequiresBothSemanticAnchors()
    {
        var command =
            new CreateSupplyConnectionCommand(
                Supply());

        Assert.Throws<ArgumentException>(
            () =>
                ElectricalCommandProposalFactory.Create(
                    command,
                    "REV-001"));
    }

    [Fact]
    public void ProposalCreation_DoesNotMutateInput()
    {
        SingleLineInput input =
            Input();
        string before =
            SingleLineInputFingerprint.Compute(input);

        _ =
            ElectricalCommandProposalFactory.Create(
                new RemoveSupplyConnectionCommand(
                    new EntityUid("supply-1")),
                "REV-001");

        string after =
            SingleLineInputFingerprint.Compute(input);

        Assert.Equal(before, after);
    }

    [Fact]
    public void InitialElectricalCommandVocabulary_IsRepresentable()
    {
        CircuitInput circuit =
            Circuit();
        ProtectionInput protection =
            Protection(circuit);

        IElectricalCommand[] commands =
        [
            new ChangeBoardSupplyCommand(
                new EntityUid("board-1"),
                new EntityUid("supply-1")),
            new ChangeSupplyCircuitCommand(
                new EntityUid("supply-1"),
                circuit.Uid),
            new ChangeProtectionCommand(
                protection.Uid,
                protection),
            new ChangeConductorCommand(
                circuit.Uid,
                circuit.Conductor),
            new ChangeCircuitDataCommand(
                circuit.Uid,
                circuit),
            new CreateSupplyConnectionCommand(
                Supply()),
            new RemoveSupplyConnectionCommand(
                new EntityUid("supply-1"))
        ];

        Assert.Equal(7, commands.Length);
        Assert.All(
            commands,
            command =>
                Assert.NotEqual(
                    CommandImpact.PresentationOnly,
                    command.Impact));
    }

    private static ElectricalAnchorEndpoint Endpoint(
        string uid,
        EntityKind kind,
        string anchorId,
        AnchorRole role) =>
        new(
            new EntityReference(
                new EntityUid(uid),
                kind),
            anchorId,
            role);

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

    private static CircuitInput Circuit() =>
        new(
            new EntityUid("circuit-1"),
            new EntityUid("board-1"),
            1,
            "C01",
            "Circuit 1",
            CircuitRole.Final,
            "LOAD",
            "1F",
            230m,
            1m,
            10m,
            "EMT",
            "A1",
            new ConductorInput(
                "CU",
                "THHN",
                2.5m,
                2.5m,
                2,
                null),
            OperationalState.Active,
            DataState.Complete);

    private static ProtectionInput Protection(
        CircuitInput circuit) =>
        new(
            new EntityUid("protection-1"),
            new EntityReference(
                circuit.Uid,
                EntityKind.Circuit),
            new EntityReference(
                circuit.Uid,
                EntityKind.Circuit),
            ProtectionKind.Breaker,
            ProtectionRole.Branch,
            2,
            16m,
            6m,
            "C",
            null,
            null,
            "TEST",
            "P1",
            OperationalState.Active,
            DataState.Complete);

    private static SingleLineInput Input()
    {
        CircuitInput circuit =
            Circuit();

        return new SingleLineInput(
            new ProjectInput(
                new EntityUid("project-1"),
                "P1",
                "Project",
                OperationalState.Active),
            [
                new SourceInput(
                    new EntityUid("source-1"),
                    "S1",
                    "Source",
                    SourceKind.Utility,
                    "1F",
                    230m,
                    1,
                    true,
                    OperationalState.Active,
                    DataState.Complete)
            ],
            [
                new BoardInput(
                    new EntityUid("board-1"),
                    1,
                    "T1",
                    "Board",
                    BoardRole.Main,
                    null,
                    230m,
                    1,
                    OperationalState.Active,
                    DataState.Complete)
            ],
            [],
            [circuit],
            [Supply()],
            [Protection(circuit)],
            [],
            [],
            new SingleLineInputMetadata(
                "1",
                "TEST",
                "interaction"));
    }
}
