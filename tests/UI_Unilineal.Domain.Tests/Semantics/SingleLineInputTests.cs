using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Semantics;

public sealed partial class SingleLineInputTests
{
    [Fact]
    public void CircuitIdentity_IsIndependentFromHumanReadableCode()
    {
        var boardUid = new EntityUid("BOARD-1");
        var circuitUid = new EntityUid("CIRCUIT-1");
        var circuit = new CircuitInput(
            circuitUid, boardUid, 1, "C01", "Alumbrado", CircuitRole.Final,
            "ALUMBRADO", "1F", 230m, 1m, 10m, null, null,
            new ConductorInput("CU", "THHN", 2.5m, 2.5m, 2, null),
            OperationalState.Active, DataState.Complete);

        Assert.Equal(circuitUid, circuit.Uid);
        Assert.Equal("C01", circuit.Code);
    }

    [Fact]
    public void MissingElectricalValues_RemainNull()
    {
        var source = new SourceInput(
            new EntityUid("SOURCE-1"), "S1", "Fuente", SourceKind.Utility,
            null, null, null, null, OperationalState.Active, DataState.Incomplete);

        Assert.Null(source.NominalVoltageV);
        Assert.Null(source.PhaseCount);
        Assert.Null(source.NeutralAvailable);
    }
    [Fact]
    public void SupplyConnection_RepresentsBoardCircuitDestinationExplicitly()
    {
        var board = new EntityReference(new EntityUid("B1"), EntityKind.Board);
        var supply = new SupplyConnection(
            new EntityUid("SC1"), board, new EntityUid("C4"), new EntityUid("B2"),
            SupplyRole.Normal, 0, true, OperationalState.Active, DataState.Complete);

        Assert.Equal(EntityKind.Board, supply.Origin.Kind);
        Assert.Equal(new EntityUid("C4"), supply.ThroughCircuitUid);
        Assert.Equal(new EntityUid("B2"), supply.DestinationBoardUid);
    }

    [Fact]
    public void Protection_HasSeparateOwnerAndProtectedEntity()
    {
        var owner = new EntityReference(new EntityUid("B1"), EntityKind.Board);
        var protects = new EntityReference(new EntityUid("BUS:B1:MAIN"), EntityKind.Bus);
        var protection = new ProtectionInput(
            new EntityUid("P1"), owner, protects, ProtectionKind.Breaker, ProtectionRole.Main,
            3, 40m, 6m, "C", null, null, null, null,
            OperationalState.Active, DataState.Complete);

        Assert.NotEqual(protection.Owner, protection.Protects);
    }
}
