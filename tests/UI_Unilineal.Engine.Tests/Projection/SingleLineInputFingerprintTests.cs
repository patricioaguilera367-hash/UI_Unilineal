using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Projection;

public sealed class SingleLineInputFingerprintTests
{
    [Fact]
    public void Compute_IsStableAcrossCollectionPermutations()
    {
        SingleLineInput input = CreateFixture();
        SingleLineInput reversed = ReverseCollections(input);

        string first = SingleLineInputFingerprint.Compute(input);
        string second = SingleLineInputFingerprint.Compute(reversed);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.Equal(first.ToUpperInvariant(), first);
        Assert.All(first, character => Assert.True(
            character is >= '0' and <= '9' or >= 'A' and <= 'F'));
    }

    [Fact]
    public void Compute_ChangesWhenSemanticContentChanges()
    {
        SingleLineInput input = CreateFixture();
        CircuitInput changedCircuit = input.Circuits[0] with { Name = "Nombre modificado" };
        var changed = new SingleLineInput(
            input.Project,
            input.Sources,
            input.Boards,
            input.Buses,
            [changedCircuit, input.Circuits[1]],
            input.SupplyConnections,
            input.Protections,
            input.Grounding,
            input.Results,
            input.Metadata);

        Assert.NotEqual(
            SingleLineInputFingerprint.Compute(input),
            SingleLineInputFingerprint.Compute(changed));
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
            input.Metadata);

    private static SingleLineInput CreateFixture()
    {
        var project = new ProjectInput(new EntityUid("P-FP"), "P-FP", "Fingerprint", OperationalState.Active);
        var s1 = new SourceInput(new EntityUid("S1"), "S1", "Fuente 1", SourceKind.Utility,
            "3F", 400m, 3, true, OperationalState.Active, DataState.Complete);
        var s2 = new SourceInput(new EntityUid("S2"), "S2", "Fuente 2", SourceKind.Generator,
            "3F", 400m, 3, true, OperationalState.Active, DataState.Complete);
        var b1 = new BoardInput(new EntityUid("B1"), 1, "T1", "Tablero 1", BoardRole.Main,
            null, 400m, 3, OperationalState.Active, DataState.Complete);
        var b2 = new BoardInput(new EntityUid("B2"), 2, "T2", "Tablero 2", BoardRole.Distribution,
            null, 400m, 3, OperationalState.Active, DataState.Complete);
        var bus1 = new BusInput(new EntityUid("BUS1"), b1.Uid, "MAIN", BusRole.Main,
            100m, OperationalState.Active, DataState.Complete);
        var bus2 = new BusInput(new EntityUid("BUS2"), b2.Uid, "MAIN", BusRole.Main,
            63m, OperationalState.Active, DataState.Complete);
        var c1 = Circuit(b1, "C1", 1);
        var c2 = Circuit(b2, "C2", 2);
        var sc1 = new SupplyConnection(new EntityUid("SC1"),
            new EntityReference(s1.Uid, EntityKind.Source), null, b1.Uid,
            SupplyRole.Normal, 0, true, OperationalState.Active, DataState.Complete);
        var sc2 = new SupplyConnection(new EntityUid("SC2"),
            new EntityReference(s2.Uid, EntityKind.Source), null, b2.Uid,
            SupplyRole.Emergency, 1, false, OperationalState.Active, DataState.Complete);
        var p1 = Protection(c1, "PR1");
        var p2 = Protection(c2, "PR2");
        var g1 = Grounding(b1, "G1", 10m);
        var g2 = Grounding(b2, "G2", 12m);
        var r1 = Result(c1, "E1");
        var r2 = Result(c2, "E2");

        return new SingleLineInput(
            project,
            [s1, s2],
            [b1, b2],
            [bus1, bus2],
            [c1, c2],
            [sc1, sc2],
            [p1, p2],
            [g1, g2],
            [r1, r2],
            new SingleLineInputMetadata("1", "TEST", "fingerprint"));
    }

    private static CircuitInput Circuit(BoardInput board, string uid, int number) =>
        new(
            new EntityUid(uid), board.Uid, number, $"C{number:D2}", $"Circuito {number}",
            CircuitRole.Final, "CARGA", "1F", 230m, 1m, 10m + number,
            "EMT", "A1", new ConductorInput("CU", "THHN", 2.5m, 2.5m, 2, null),
            OperationalState.Active, DataState.Complete);

    private static ProtectionInput Protection(CircuitInput circuit, string uid) =>
        new(
            new EntityUid(uid),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            ProtectionKind.Breaker, ProtectionRole.Branch,
            2, 16m, 6m, "C", null, null, "TEST", uid,
            OperationalState.Active, DataState.Complete);

    private static GroundingInput Grounding(BoardInput board, string uid, decimal resistance) =>
        new(
            new EntityUid(uid),
            new EntityReference(board.Uid, EntityKind.Board),
            GroundingKind.Protection, "CU", 16m, resistance,
            "METHOD", "INSTRUMENT", OperationalState.Active, DataState.Complete);

    private static ElectricalResultInput Result(CircuitInput circuit, string execution) =>
        new(
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            1000m, 4.35m, 4.8m, 20m, 1.5m, 0.65m,
            "OK", ResultState.Current, execution);
}
