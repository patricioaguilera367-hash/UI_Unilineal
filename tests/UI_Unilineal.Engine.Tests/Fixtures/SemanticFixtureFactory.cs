using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Tests.Fixtures;

internal static class SemanticFixtureFactory
{
    public static SingleLineInput Minimal()
    {
        var project = new ProjectInput(new EntityUid("P1"), "P1", "Proyecto mínimo", OperationalState.Active);
        var source = new SourceInput(new EntityUid("S1"), "EMPALME", "Empalme", SourceKind.Utility,
            "3F", 400m, 3, true, OperationalState.Active, DataState.Complete);
        var board = new BoardInput(new EntityUid("B1"), 1, "TGBT", "Tablero general", BoardRole.Main,
            "Sala eléctrica", 400m, 3, OperationalState.Active, DataState.Complete);
        var circuit = new CircuitInput(new EntityUid("C1"), board.Uid, 1, "C01", "Alumbrado", CircuitRole.Final,
            "ALUMBRADO", "1F", 230m, 1m, 10m, null, null,
            new ConductorInput("CU", "THHN", 2.5m, 2.5m, 2, null),
            OperationalState.Active, DataState.Complete);
        var supply = new SupplyConnection(new EntityUid("SC1"),
            new EntityReference(source.Uid, EntityKind.Source), null, board.Uid,
            SupplyRole.Normal, 0, true, OperationalState.Active, DataState.Complete);
        var protection = new ProtectionInput(new EntityUid("PR1"),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            ProtectionKind.Breaker, ProtectionRole.Branch, 2, 10m, 6m, "C",
            null, null, null, null, OperationalState.Active, DataState.Complete);
        var result = new ElectricalResultInput(
            new EntityReference(circuit.Uid, EntityKind.Circuit), 1800m, 7.83m, 8.61m,
            20m, 2m, 0.87m, "OK", ResultState.Current, "EXEC-1");

        return new SingleLineInput(project, [source], [board], [], [circuit], [supply], [protection], [], [result],
            new SingleLineInputMetadata("1", "TEST", "minimal"));
    }
}
