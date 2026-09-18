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
}
