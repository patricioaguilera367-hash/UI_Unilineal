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

    public static SingleLineInput BoardChain(int boardCount)
    {
        if (boardCount is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(boardCount));
        }

        var project = new ProjectInput(
            new EntityUid("P1"),
            "P1",
            $"Cadena {boardCount}",
            OperationalState.Active);
        var source = new SourceInput(
            new EntityUid("S1"),
            "EMPALME",
            "Empalme",
            SourceKind.Utility,
            "3F",
            400m,
            3,
            true,
            OperationalState.Active,
            DataState.Complete);

        var boards = new List<BoardInput>(boardCount);
        var circuits = new List<CircuitInput>(boardCount);
        var supplies = new List<SupplyConnection>(boardCount);
        var protections = new List<ProtectionInput>(boardCount);

        for (int index = 1; index <= boardCount; index++)
        {
            string suffix = index.ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            var board = new BoardInput(
                new EntityUid($"B{suffix}"),
                index,
                $"TD{suffix}",
                $"Tablero {suffix}",
                index == 1 ? BoardRole.Main : BoardRole.Distribution,
                null,
                400m,
                3,
                OperationalState.Active,
                DataState.Complete);
            boards.Add(board);

            var circuit = new CircuitInput(
                new EntityUid($"F{suffix}"),
                board.Uid,
                index,
                $"F{suffix}",
                $"Alimentador {suffix}",
                CircuitRole.Feeder,
                "ALIMENTADOR",
                "3F",
                400m,
                1m,
                10m + index,
                null,
                null,
                new ConductorInput("CU", "THHN", 6m, 6m, 4, null),
                OperationalState.Active,
                DataState.Complete);
            circuits.Add(circuit);

            protections.Add(new ProtectionInput(
                new EntityUid($"PR{suffix}"),
                new EntityReference(circuit.Uid, EntityKind.Circuit),
                new EntityReference(circuit.Uid, EntityKind.Circuit),
                ProtectionKind.Breaker,
                ProtectionRole.Feeder,
                3,
                25m,
                10m,
                "C",
                null,
                null,
                null,
                null,
                OperationalState.Active,
                DataState.Complete));
        }

        supplies.Add(new SupplyConnection(
            new EntityUid("SC000"),
            new EntityReference(source.Uid, EntityKind.Source),
            null,
            boards[0].Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete));

        for (int index = 0; index < boardCount - 1; index++)
        {
            string suffix = (index + 1).ToString("D3", System.Globalization.CultureInfo.InvariantCulture);
            supplies.Add(new SupplyConnection(
                new EntityUid($"SC{suffix}"),
                new EntityReference(boards[index].Uid, EntityKind.Board),
                circuits[index].Uid,
                boards[index + 1].Uid,
                SupplyRole.Normal,
                0,
                true,
                OperationalState.Active,
                DataState.Complete));
        }

        return new SingleLineInput(
            project,
            [source],
            boards,
            [],
            circuits,
            supplies,
            protections,
            [],
            [],
            new SingleLineInputMetadata("1", "TEST", $"chain-{boardCount}"));
    }
}
