using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class BoardDetailCompositionBuilderTests
{
    [Fact]
    public void BuildBoardDetail_PreservesVariableProtectionChainAndKinds()
    {
        SingleLineInput input = NestedWithDifferential();
        SingleLineProjection projection = Project(input);
        var boardUid = new EntityUid("B2");

        DrawingComposition composition =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                boardUid,
                Profile());

        string branchId = CompositionIdFactory.DetailBranch(
            boardUid,
            new EntityUid("C2"));
        CompositionBlock[] protections = composition.Blocks
            .Where(x =>
                x.ParentId == branchId &&
                x.SemanticRole is "Protection" or "DifferentialProtection")
            .ToArray();

        Assert.Equal(2, protections.Length);
        Assert.Contains(
            protections,
            x => x.SymbolOverrides.TryGetValue("PROTECTION", out string? symbol) &&
                 symbol == "BREAKER_2X");
        Assert.Contains(
            protections,
            x => x.SymbolOverrides.TryGetValue("PROTECTION", out string? symbol) &&
                 symbol == "RCD_2X");
    }

    [Fact]
    public void BuildBoardDetail_ServiceEntranceAssemblyKeepsManualMeterAndProtectionData()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        SourceInput utility = Assert.Single(source.Sources);

        var serviceProtection =
            new ProtectionInput(
                new EntityUid("PR-SERVICE"),
                new EntityReference(
                    utility.Uid,
                    EntityKind.Source),
                new EntityReference(
                    utility.Uid,
                    EntityKind.Source),
                ProtectionKind.Breaker,
                ProtectionRole.Main,
                2,
                25m,
                6m,
                "C",
                null,
                null,
                null,
                null,
                OperationalState.Active,
                DataState.Complete);
        var serviceEntrance =
            new ServiceEntranceInput(
                new EntityUid("SE1"),
                utility.Uid,
                MeterKind.SinglePhase,
                "BT1",
                "Cliente residencial",
                serviceProtection.Uid,
                InputValueAuthority.Manual,
                OperationalState.Active,
                DataState.Complete);

        SingleLineInput input = Rebuild(
            source,
            protections: [.. source.Protections, serviceProtection],
            serviceEntrances: [serviceEntrance]);
        SingleLineProjection projection = Project(input);

        DrawingComposition composition =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                new EntityUid("B1"),
                Profile());

        CompositionBlock empalme = Assert.Single(
            composition.Blocks,
            block =>
                block.BlockDefinitionId ==
                "SERVICE_ENTRANCE_ASSEMBLY_BLOCK");

        Assert.Equal(
            "EMPALME",
            empalme.Labels["TITLE"]);
        Assert.Equal(
            "M 1F",
            empalme.Labels["METER_CODE"]);
        Assert.Contains(
            "BT1",
            empalme.Labels["TARIFF"]);
        Assert.Contains(
            "Manual",
            empalme.Labels["PROTECTION_TEXT"]);
        Assert.Equal(
            "BREAKER_2X",
            empalme.SymbolOverrides["PROTECTION"]);
    }

    [Fact]
    public void BuildBoardDetail_MapsDownstreamBoardAndFinalLoadDestinations()
    {
        SingleLineProjection projection = Project(SemanticFixtureFactory.NestedBoards());
        RIC18DrawingProfile profile = Profile();

        DrawingComposition main =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                new EntityUid("B1"),
                profile);
        DrawingComposition downstream =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                new EntityUid("B2"),
                profile);

        Assert.Contains(
            main.Blocks,
            x => x.Id == CompositionIdFactory.DetailDestination(
                     new EntityUid("B1"),
                     new EntityUid("C4")) &&
                 x.BlockDefinitionId == "DOWNSTREAM_BOARD_BLOCK");
        Assert.Contains(
            downstream.Blocks,
            x => x.Id == CompositionIdFactory.DetailDestination(
                     new EntityUid("B2"),
                     new EntityUid("C2")) &&
                 x.BlockDefinitionId == "FINAL_LOAD_BLOCK");
    }

    [Fact]
    public void BuildBoardDetail_GroundingUsesGroundAnchors()
    {
        SingleLineInput input = NestedWithGrounding();
        SingleLineProjection projection = Project(input);
        var boardUid = new EntityUid("B2");
        RIC18DrawingProfile profile = Profile();

        DrawingComposition composition =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                boardUid,
                profile);

        Assert.Contains(
            profile.Symbols.Single(x => x.Id == "BUS").Anchors,
            x => x.Role == AnchorRole.Ground);

        CompositionBlock grounding = Assert.Single(
            composition.Blocks,
            x => x.BlockDefinitionId == "GROUNDING_BLOCK");
        Assert.Equal(
            CompositionIdFactory.DetailGrounding(
                boardUid,
                new EntityUid("G1")),
            grounding.Id);

        CompositionConnection connection = Assert.Single(
            composition.Connections,
            x => x.SemanticEntity ==
                new EntityReference(new EntityUid("G1"), EntityKind.Grounding));

        Assert.Equal(AnchorRole.Ground, connection.Source.Role);
        Assert.Equal(AnchorRole.Ground, connection.Target.Role);
    }

    [Fact]
    public void BuildBoardDetail_ContainsIncomingBusBranchAndDestination()
    {
        SingleLineProjection projection = Project(SemanticFixtureFactory.Minimal());
        var boardUid = new EntityUid("B1");

        DrawingComposition composition =
            new CompositionBuilder().BuildBoardDetail(
                projection,
                boardUid,
                Profile());

        Assert.Equal(DrawingCompositionKind.BoardDetail, composition.Kind);
        Assert.Equal(
            new EntityReference(boardUid, EntityKind.Board),
            composition.Scope);
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "INCOMING_SUPPLY_BLOCK");
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "MAIN_BUS_BLOCK");
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "NEUTRAL_BUS_BLOCK");
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "PE_BUS_BLOCK");
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "CIRCUIT_BRANCH_BLOCK");

        Assert.Contains(
            composition.Connections,
            x =>
                x.LineStyleId == "NEUTRAL_AUX" &&
                x.Source.Role == AnchorRole.Neutral);
        Assert.Contains(
            composition.Connections,
            x =>
                x.LineStyleId == "GROUND_AUX" &&
                x.Source.Role == AnchorRole.Ground);
        Assert.Contains(
            composition.Blocks,
            x => x.BlockDefinitionId == "FINAL_LOAD_BLOCK");
    }

    private static SingleLineInput NestedWithDifferential()
    {
        SingleLineInput source = SemanticFixtureFactory.NestedBoards();
        CircuitInput circuit = source.Circuits.Single(x => x.Uid == new EntityUid("C2"));
        var differential = new ProtectionInput(
            new EntityUid("PR2-RCD"),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            new EntityReference(circuit.Uid, EntityKind.Circuit),
            ProtectionKind.Differential,
            ProtectionRole.Branch,
            2,
            25m,
            null,
            null,
            30m,
            "A",
            null,
            null,
            OperationalState.Active,
            DataState.Complete);

        return Rebuild(
            source,
            protections: [.. source.Protections, differential]);
    }

    private static SingleLineInput NestedWithGrounding()
    {
        SingleLineInput source = SemanticFixtureFactory.NestedBoards();
        var grounding = new GroundingInput(
            new EntityUid("G1"),
            new EntityReference(new EntityUid("B2"), EntityKind.Board),
            GroundingKind.Protection,
            "CU",
            16m,
            5m,
            "TEST",
            "TEST",
            OperationalState.Active,
            DataState.Complete);

        return Rebuild(source, grounding: [grounding]);
    }

    private static SingleLineInput Rebuild(
        SingleLineInput source,
        IEnumerable<ProtectionInput>? protections = null,
        IEnumerable<GroundingInput>? grounding = null,
        IEnumerable<ServiceEntranceInput>? serviceEntrances = null) =>
        new(
            source.Project,
            source.Sources,
            source.Boards,
            source.Buses,
            source.Circuits,
            source.SupplyConnections,
            protections ?? source.Protections,
            grounding ?? source.Grounding,
            source.Results,
            source.Metadata,
            serviceEntrances ?? source.ServiceEntrances);

    private static SingleLineProjection Project(SingleLineInput input)
    {
        ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(input);
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Validation.Issues));
        return Assert.IsType<SingleLineProjection>(result.Projection);
    }

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
