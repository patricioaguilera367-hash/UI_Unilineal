using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Validation;

public sealed class SingleLineInputValidatorTests
{
    [Fact]
    public void Validate_DuplicateBoardUid_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput board = source.Boards[0];
        SingleLineInput invalid = Rebuild(source, boards: [board, board]);

        InputValidationResult result = new SingleLineInputValidator().Validate(invalid);

        AssertError(result, ValidationCodes.DuplicateEntityUid);
    }

    [Fact]
    public void Validate_CircuitBoardMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        CircuitInput circuit = source.Circuits[0] with { BoardUid = new EntityUid("MISSING-BOARD") };

        InputValidationResult result = Validate(Rebuild(source, circuits: [circuit]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_BusBoardMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        var bus = new BusInput(
            new EntityUid("BUS1"), new EntityUid("MISSING-BOARD"), "MAIN",
            BusRole.Main, 100m, OperationalState.Active, DataState.Complete);

        InputValidationResult result = Validate(Rebuild(source, buses: [bus]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_SupplyDestinationMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        SupplyConnection supply = source.SupplyConnections[0] with
        {
            DestinationBoardUid = new EntityUid("MISSING-BOARD")
        };

        InputValidationResult result = Validate(Rebuild(source, supplies: [supply]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_SupplyOriginMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        SupplyConnection supply = source.SupplyConnections[0] with
        {
            Origin = new EntityReference(new EntityUid("MISSING-SOURCE"), EntityKind.Source)
        };

        InputValidationResult result = Validate(Rebuild(source, supplies: [supply]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_SupplyThroughCircuitMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput second = SecondBoard();
        var supply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(source.Boards[0].Uid, EntityKind.Board),
            new EntityUid("MISSING-CIRCUIT"),
            second.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);

        InputValidationResult result = Validate(Rebuild(
            source,
            boards: [source.Boards[0], second],
            supplies: [source.SupplyConnections[0], supply]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_InvalidSupplyOriginKind_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        SupplyConnection supply = source.SupplyConnections[0] with
        {
            Origin = new EntityReference(source.Circuits[0].Uid, EntityKind.Circuit)
        };

        InputValidationResult result = Validate(Rebuild(source, supplies: [supply]));

        AssertError(result, ValidationCodes.InvalidSupplyOrigin);
    }

    [Fact]
    public void Validate_BoardOriginWithoutThroughCircuit_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput second = SecondBoard();
        var supply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(source.Boards[0].Uid, EntityKind.Board),
            null,
            second.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);

        InputValidationResult result = Validate(Rebuild(
            source,
            boards: [source.Boards[0], second],
            supplies: [source.SupplyConnections[0], supply]));

        AssertError(result, ValidationCodes.BoardSupplyRequiresCircuit);
    }

    [Fact]
    public void Validate_SourceOriginWithThroughCircuit_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        SupplyConnection supply = source.SupplyConnections[0] with
        {
            ThroughCircuitUid = source.Circuits[0].Uid
        };

        InputValidationResult result = Validate(Rebuild(source, supplies: [supply]));

        AssertError(result, ValidationCodes.SourceSupplyCannotUseBoardCircuit);
    }

    [Fact]
    public void Validate_BoardOriginCircuitOwnedByAnotherBoard_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput second = SecondBoard();
        CircuitInput secondCircuit = CircuitForBoard(second, "C2");
        var supply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(source.Boards[0].Uid, EntityKind.Board),
            secondCircuit.Uid,
            second.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);

        InputValidationResult result = Validate(Rebuild(
            source,
            boards: [source.Boards[0], second],
            circuits: [source.Circuits[0], secondCircuit],
            supplies: [source.SupplyConnections[0], supply]));

        AssertError(result, ValidationCodes.SupplyCircuitOwnerMismatch);
    }

    [Fact]
    public void Validate_BoardSuppliesItself_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        var supply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(source.Boards[0].Uid, EntityKind.Board),
            source.Circuits[0].Uid,
            source.Boards[0].Uid,
            SupplyRole.Normal,
            0,
            false,
            OperationalState.Inactive,
            DataState.Complete);

        InputValidationResult result = Validate(Rebuild(
            source,
            supplies: [source.SupplyConnections[0], supply]));

        AssertError(result, ValidationCodes.SelfSupply);
    }

    [Fact]
    public void Validate_NormallyActiveBoardSupplyCycle_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput second = SecondBoard();
        CircuitInput secondCircuit = CircuitForBoard(second, "C2");

        var toSecond = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(source.Boards[0].Uid, EntityKind.Board),
            source.Circuits[0].Uid,
            second.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);
        var backToFirst = new SupplyConnection(
            new EntityUid("SC3"),
            new EntityReference(second.Uid, EntityKind.Board),
            secondCircuit.Uid,
            source.Boards[0].Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);

        InputValidationResult result = Validate(Rebuild(
            source,
            boards: [source.Boards[0], second],
            circuits: [source.Circuits[0], secondCircuit],
            supplies: [source.SupplyConnections[0], toSecond, backToFirst]));

        AssertError(result, ValidationCodes.SupplyCycle);
    }

    [Fact]
    public void Validate_MultipleMainBusesOnBoard_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        var first = new BusInput(
            new EntityUid("BUS1"), source.Boards[0].Uid, "MAIN-1",
            BusRole.Main, 100m, OperationalState.Active, DataState.Complete);
        var second = new BusInput(
            new EntityUid("BUS2"), source.Boards[0].Uid, "MAIN-2",
            BusRole.Main, 100m, OperationalState.Active, DataState.Complete);

        InputValidationResult result = Validate(Rebuild(source, buses: [first, second]));

        AssertError(result, ValidationCodes.MultipleMainBuses);
    }

    [Fact]
    public void Validate_ProtectionOwnerMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        ProtectionInput protection = source.Protections[0] with
        {
            Owner = new EntityReference(new EntityUid("MISSING-CIRCUIT"), EntityKind.Circuit)
        };

        InputValidationResult result = Validate(Rebuild(source, protections: [protection]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_ProtectionProtectedEntityMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        ProtectionInput protection = source.Protections[0] with
        {
            Protects = new EntityReference(new EntityUid("MISSING-CIRCUIT"), EntityKind.Circuit)
        };

        InputValidationResult result = Validate(Rebuild(source, protections: [protection]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_GroundingOwnerMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        var grounding = new GroundingInput(
            new EntityUid("G1"),
            new EntityReference(new EntityUid("MISSING-BOARD"), EntityKind.Board),
            GroundingKind.Protection,
            "CU",
            16m,
            5m,
            "METHOD",
            "INSTRUMENT",
            OperationalState.Active,
            DataState.Complete);

        InputValidationResult result = Validate(Rebuild(source, grounding: [grounding]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_ResultEntityMissing_ReturnsMissingReferenceError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        var resultInput = new ElectricalResultInput(
            new EntityReference(new EntityUid("MISSING-CIRCUIT"), EntityKind.Circuit),
            null, null, null, null, null, null, null,
            ResultState.Missing, null);

        InputValidationResult result = Validate(Rebuild(source, results: [resultInput]));

        AssertError(result, ValidationCodes.MissingReference);
    }

    [Fact]
    public void Validate_BoardWithoutIncomingSupply_ReturnsWarningOnly()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();

        InputValidationResult result = Validate(Rebuild(source, supplies: []));

        AssertWarningOnly(result, ValidationCodes.BoardWithoutSupply);
    }

    [Fact]
    public void Validate_ActiveCircuitWithoutConductor_ReturnsWarningOnly()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        CircuitInput circuit = source.Circuits[0] with { Conductor = null };

        InputValidationResult result = Validate(Rebuild(source, circuits: [circuit]));

        AssertWarningOnly(result, ValidationCodes.CircuitMissingConductor);
    }

    [Fact]
    public void Validate_ActiveCircuitWithoutOvercurrentProtection_ReturnsWarningOnly()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();

        InputValidationResult result = Validate(Rebuild(source, protections: []));

        AssertWarningOnly(result, ValidationCodes.CircuitMissingProtection);
    }

    [Fact]
    public void Validate_BoardWithoutNominalVoltage_ReturnsWarningOnly()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput board = source.Boards[0] with { NominalVoltageV = null };

        InputValidationResult result = Validate(Rebuild(source, boards: [board]));

        AssertWarningOnly(result, ValidationCodes.BoardMissingVoltage);
    }

    [Fact]
    public void Validate_IncompleteSource_ReturnsWarningOnly()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        SourceInput supplySource = source.Sources[0] with { PhaseCount = null };

        InputValidationResult result = Validate(Rebuild(source, sources: [supplySource]));

        AssertWarningOnly(result, ValidationCodes.SourceIncomplete);
    }

    [Fact]
    public void Validate_GroundingWithoutSectionOrResistance_ReturnsWarningOnly()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        var grounding = new GroundingInput(
            new EntityUid("G1"),
            new EntityReference(source.Boards[0].Uid, EntityKind.Board),
            GroundingKind.Protection,
            "CU",
            null,
            null,
            "METHOD",
            "INSTRUMENT",
            OperationalState.Active,
            DataState.Incomplete);

        InputValidationResult result = Validate(Rebuild(source, grounding: [grounding]));

        AssertWarningOnly(result, ValidationCodes.GroundingIncomplete);
    }

    [Fact]
    public void Validate_Issues_AreDeterministicallySorted()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput board = source.Boards[0] with { NominalVoltageV = null };
        CircuitInput circuit = source.Circuits[0] with { Conductor = null };

        InputValidationResult result = Validate(Rebuild(
            source,
            boards: [board],
            circuits: [circuit],
            protections: [],
            supplies: []));

        string[] actual = result.Issues.Select(x => x.Code).ToArray();
        string[] expected = actual
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    private static InputValidationResult Validate(SingleLineInput input) =>
        new SingleLineInputValidator().Validate(input);

    private static void AssertError(InputValidationResult result, string code) =>
        Assert.Contains(
            result.Issues,
            issue => issue.Code == code && issue.Severity == ValidationSeverity.Error);

    private static void AssertWarningOnly(InputValidationResult result, string code)
    {
        Assert.False(result.HasErrors);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == code && issue.Severity == ValidationSeverity.Warning);
    }

    private static BoardInput SecondBoard() =>
        new(
            new EntityUid("B2"),
            2,
            "TD2",
            "Tablero secundario",
            BoardRole.Distribution,
            null,
            400m,
            3,
            OperationalState.Active,
            DataState.Complete);

    private static CircuitInput CircuitForBoard(BoardInput board, string uid) =>
        new(
            new EntityUid(uid),
            board.Uid,
            2,
            "F02",
            "Alimentador",
            CircuitRole.Feeder,
            "ALIMENTADOR",
            "3F",
            400m,
            1m,
            15m,
            null,
            null,
            new ConductorInput("CU", "THHN", 6m, 6m, 4, null),
            OperationalState.Active,
            DataState.Complete);

    private static SingleLineInput Rebuild(
        SingleLineInput source,
        IEnumerable<SourceInput>? sources = null,
        IEnumerable<BoardInput>? boards = null,
        IEnumerable<BusInput>? buses = null,
        IEnumerable<CircuitInput>? circuits = null,
        IEnumerable<SupplyConnection>? supplies = null,
        IEnumerable<ProtectionInput>? protections = null,
        IEnumerable<GroundingInput>? grounding = null,
        IEnumerable<ElectricalResultInput>? results = null) =>
        new(
            source.Project,
            sources ?? source.Sources,
            boards ?? source.Boards,
            buses ?? source.Buses,
            circuits ?? source.Circuits,
            supplies ?? source.SupplyConnections,
            protections ?? source.Protections,
            grounding ?? source.Grounding,
            results ?? source.Results,
            source.Metadata);
}
