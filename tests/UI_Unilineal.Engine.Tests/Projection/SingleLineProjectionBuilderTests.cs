using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Projection;

public sealed class SingleLineProjectionBuilderTests
{
    [Fact]
    public void ProjectionContracts_RepresentSummaryAndBoardDetailWithoutGeometry()
    {
        var projectUid = new EntityUid("P1");
        var source = new SourceInput(
            new EntityUid("S1"), "EMPALME", "Empalme", SourceKind.Utility,
            "3F", 400m, 3, true, OperationalState.Active, DataState.Complete);
        var board = new BoardInput(
            new EntityUid("B1"), 1, "TGBT", "Tablero", BoardRole.Main,
            null, 400m, 3, OperationalState.Active, DataState.Complete);
        var circuit = new CircuitInput(
            new EntityUid("C1"), board.Uid, 1, "C01", "Circuito", CircuitRole.Final,
            "ALUMBRADO", "1F", 230m, 1m, 10m, null, null,
            new ConductorInput("CU", "THHN", 2.5m, 2.5m, 2, null),
            OperationalState.Active, DataState.Complete);
        var supply = new SupplyConnection(
            new EntityUid("SC1"),
            new EntityReference(source.Uid, EntityKind.Source),
            null,
            board.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);
        var protection = new ProtectionInput(
            new EntityUid("PR1"),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            ProtectionKind.Breaker,
            ProtectionRole.Branch,
            2, 10m, 6m, "C", null, null, null, null,
            OperationalState.Active,
            DataState.Complete);
        var resultInput = new ElectricalResultInput(
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            1800m, 7.83m, 8.61m, 20m, 2m, 0.87m,
            "OK", ResultState.Current, "EXEC-1");
        var busInput = new BusInput(
            new EntityUid("BUS:B1:MAIN"), board.Uid, "MAIN", BusRole.Main,
            null, OperationalState.Active, DataState.Complete);

        var issue = new ProjectionIssue(
            "TEST", ValidationSeverity.Warning, "Test",
            new EntityReference(board.Uid, EntityKind.Board), null);
        var summaryNode = new SummaryNode(
            new EntityReference(board.Uid, EntityKind.Board),
            board.Code, board.Name, board.Role, ProjectionStatus.Warning, 0, 1);
        var summaryConnection = new SummaryConnection(
            supply.Uid, supply.Origin, supply.ThroughCircuitUid,
            new EntityReference(board.Uid, EntityKind.Board),
            supply.Role, supply.IsNormallyActive, ProjectionStatus.Ok);
        var summary = new SummaryProjection(
            [summaryNode], [summaryConnection],
            [new EntityReference(source.Uid, EntityKind.Source)], [issue]);

        var incoming = new IncomingSupplyProjection(
            supply,
            source,
            null,
            null,
            null,
            null,
            ProjectionStatus.Ok);
        var bus = new BusProjection(
            busInput, [], ProjectionStatus.Ok, 0);
        var destination = new BranchDestination(
            DestinationKind.Load,
            new EntityReference(new EntityUid("LOAD:C1"), EntityKind.Load),
            "Carga",
            null);
        var branch = new BranchProjection(
            circuit, [protection], resultInput, destination,
            BranchKind.FinalCircuit, ProjectionStatus.Ok, []);
        var detail = new BoardDetailProjection(
            board, [incoming], bus, [branch], [], [issue], []);

        var projection = new SingleLineProjection(
            projectUid, summary, [detail], [issue], "fingerprint");
        var build = new ProjectionBuildResult(
            projection, new InputValidationResult([]));

        Assert.True(build.Success);
        Assert.Equal(ProjectionStatus.Warning, summaryNode.Status);
        Assert.Equal(BranchKind.FinalCircuit, branch.Kind);
        Assert.Equal(DestinationKind.Load, destination.Kind);
    }

    [Fact]
    public void Build_ValidInput_ReturnsSummaryAndEveryBoardDetail()
    {
        SingleLineInput input = SemanticFixtureFactory.NestedBoards();

        ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(input);

        Assert.True(result.Success);
        Assert.NotNull(result.Projection);
        Assert.Equal(2, result.Projection!.BoardDetails.Count);
        Assert.Equal(input.Project.Uid, result.Projection.ProjectUid);
    }

    [Fact]
    public void Build_StructuralCycle_BlocksProjection()
    {
        SingleLineInput source = SemanticFixtureFactory.BoardChain(2);
        BoardInput first = source.Boards[0];
        BoardInput last = source.Boards[^1];
        CircuitInput lastCircuit = source.Circuits.Single(x => x.BoardUid == last.Uid);
        var cycle = new SupplyConnection(
            new EntityUid("SC-CYCLE"),
            new EntityReference(last.Uid, EntityKind.Board),
            lastCircuit.Uid,
            first.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);
        var invalid = new SingleLineInput(
            source.Project,
            source.Sources,
            source.Boards,
            source.Buses,
            source.Circuits,
            [.. source.SupplyConnections, cycle],
            source.Protections,
            source.Grounding,
            source.Results,
            source.Metadata,
            source.ServiceEntrances);

        ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(invalid);

        Assert.False(result.Success);
        Assert.Null(result.Projection);
        Assert.Contains(result.Validation.Issues, x => x.Code == ValidationCodes.SupplyCycle);
    }

    [Fact]
    public void Build_IncompleteButStructurallyValidInput_ProducesWarningProjection()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        CircuitInput circuit = source.Circuits[0] with { Conductor = null };
        var incomplete = new SingleLineInput(
            source.Project,
            source.Sources,
            source.Boards,
            source.Buses,
            [circuit],
            source.SupplyConnections,
            source.Protections,
            source.Grounding,
            source.Results,
            source.Metadata);

        ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(incomplete);

        Assert.True(result.Success);
        Assert.NotNull(result.Projection);
        Assert.Contains(
            result.Validation.Issues,
            x => x.Code == ValidationCodes.CircuitMissingConductor);
        BranchProjection branch = Assert.Single(result.Projection!.BoardDetails[0].Branches);
        Assert.Equal(ProjectionStatus.Warning, branch.Status);
    }

    [Fact]
    public void Build_PermutedInput_PreservesDeterministicProjectionOrder()
    {
        SingleLineInput source = SemanticFixtureFactory.NestedBoards();
        SingleLineInput reversed = ReverseCollections(source);

        ProjectionBuildResult first = new SingleLineProjectionBuilder().Build(source);
        ProjectionBuildResult second = new SingleLineProjectionBuilder().Build(reversed);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.NotNull(first.Projection);
        Assert.NotNull(second.Projection);

        Assert.Equal(first.Projection!.InputFingerprint, second.Projection!.InputFingerprint);
        Assert.Equal(first.Projection.Summary.Nodes, second.Projection.Summary.Nodes);
        Assert.Equal(first.Projection.Summary.Connections, second.Projection.Summary.Connections);
        Assert.Equal(
            first.Projection.BoardDetails.Select(x => x.Board.Uid),
            second.Projection.BoardDetails.Select(x => x.Board.Uid));

        for (int index = 0; index < first.Projection.BoardDetails.Count; index++)
        {
            BoardDetailProjection firstDetail = first.Projection.BoardDetails[index];
            BoardDetailProjection secondDetail = second.Projection.BoardDetails[index];

            Assert.Equal(
                firstDetail.Branches.Select(BranchSignature),
                secondDetail.Branches.Select(BranchSignature));
        }
    }

    private static SingleLineInput ReverseCollections(SingleLineInput input) =>
        new(
            input.Project,
            input.Sources.Reverse(),
            input.Boards.Reverse(),
            input.Buses.Reverse(),
            input.Circuits.Reverse(),
            input.SupplyConnections.Reverse(),
            input.Protections.Reverse(),
            input.Grounding.Reverse(),
            input.Results.Reverse(),
            input.Metadata,
            input.ServiceEntrances.Reverse());

    private static string BranchSignature(BranchProjection branch) =>
        string.Join(
            "|",
            branch.Circuit.Uid,
            branch.Kind,
            branch.Destination.Entity?.Uid.ToString() ?? "-",
            branch.Status,
            string.Join(",", branch.ProtectionChain.Select(x => x.Uid.ToString())));
}
