using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Playground.Fixtures;

public sealed record PlaygroundFixture(
    SingleLineInput Input,
    SingleLineProjection Projection,
    RIC18DrawingProfile Profile);

public static class PlaygroundFixtureFactory
{
    public static PlaygroundFixture Create()
    {
        SingleLineInput input = CreateNestedBoards();
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(input);

        SingleLineProjection projection =
            projectionResult.Projection ??
            throw new InvalidOperationException(
                "The Playground fixture did not produce a valid projection.");

        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader().LoadDirectory(
                ResolveProfileDirectory());

        return new PlaygroundFixture(
            input,
            projection,
            profile);
    }

    private static SingleLineInput CreateNestedBoards()
    {
        var project =
            new ProjectInput(
                new EntityUid("P2"),
                "P2",
                "Proyecto anidado",
                OperationalState.Active);
        var source =
            new SourceInput(
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
        var main =
            new BoardInput(
                new EntityUid("B1"),
                1,
                "TGBT",
                "Tablero general",
                BoardRole.Main,
                "Sala eléctrica",
                400m,
                3,
                OperationalState.Active,
                DataState.Complete);
        var downstream =
            new BoardInput(
                new EntityUid("B2"),
                2,
                "TDA",
                "Tablero derivado",
                BoardRole.Distribution,
                "Nivel 1",
                400m,
                3,
                OperationalState.Active,
                DataState.Complete);

        var feeder =
            new CircuitInput(
                new EntityUid("C4"),
                main.Uid,
                4,
                "C04",
                "Alimentador TDA",
                CircuitRole.Feeder,
                "ALIMENTADOR",
                "3F",
                400m,
                1m,
                25m,
                null,
                null,
                new ConductorInput(
                    "CU",
                    "THHN",
                    10m,
                    10m,
                    4,
                    null),
                OperationalState.Active,
                DataState.Complete);
        var final =
            new CircuitInput(
                new EntityUid("C2"),
                downstream.Uid,
                1,
                "C01",
                "Alumbrado",
                CircuitRole.Final,
                "ALUMBRADO",
                "1F",
                230m,
                1m,
                12m,
                null,
                null,
                new ConductorInput(
                    "CU",
                    "THHN",
                    2.5m,
                    2.5m,
                    2,
                    null),
                OperationalState.Active,
                DataState.Complete);

        var sourceSupply =
            new SupplyConnection(
                new EntityUid("SC1"),
                new EntityReference(
                    source.Uid,
                    EntityKind.Source),
                null,
                main.Uid,
                SupplyRole.Normal,
                0,
                true,
                OperationalState.Active,
                DataState.Complete);
        var downstreamSupply =
            new SupplyConnection(
                new EntityUid("SC2"),
                new EntityReference(
                    main.Uid,
                    EntityKind.Board),
                feeder.Uid,
                downstream.Uid,
                SupplyRole.Normal,
                0,
                true,
                OperationalState.Active,
                DataState.Complete);

        var serviceProtection =
            new ProtectionInput(
                new EntityUid("PR-SERVICE"),
                new EntityReference(
                    source.Uid,
                    EntityKind.Source),
                new EntityReference(
                    source.Uid,
                    EntityKind.Source),
                ProtectionKind.Breaker,
                ProtectionRole.Main,
                4,
                40m,
                10m,
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
                source.Uid,
                MeterKind.ThreePhase,
                "BT1",
                "Medidor trifásico · datos editables",
                serviceProtection.Uid,
                InputValueAuthority.Manual,
                OperationalState.Active,
                DataState.Complete);

        var feederBreaker =
            new ProtectionInput(
                new EntityUid("PR4"),
                new EntityReference(
                    feeder.Uid,
                    EntityKind.Circuit),
                new EntityReference(
                    feeder.Uid,
                    EntityKind.Circuit),
                ProtectionKind.Breaker,
                ProtectionRole.Feeder,
                3,
                32m,
                10m,
                "C",
                null,
                null,
                null,
                null,
                OperationalState.Active,
                DataState.Complete);
        var finalBreaker =
            new ProtectionInput(
                new EntityUid("PR2"),
                new EntityReference(
                    final.Uid,
                    EntityKind.Circuit),
                new EntityReference(
                    final.Uid,
                    EntityKind.Circuit),
                ProtectionKind.Breaker,
                ProtectionRole.Branch,
                2,
                10m,
                6m,
                "C",
                null,
                null,
                null,
                null,
                OperationalState.Active,
                DataState.Complete);

        var finalRcd =
            new ProtectionInput(
                new EntityUid("PR2-RCD"),
                new EntityReference(
                    final.Uid,
                    EntityKind.Circuit),
                new EntityReference(
                    final.Uid,
                    EntityKind.Circuit),
                ProtectionKind.Differential,
                ProtectionRole.Adopted,
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

        return new SingleLineInput(
            project,
            [source],
            [main, downstream],
            [],
            [feeder, final],
            [sourceSupply, downstreamSupply],
            [serviceProtection, feederBreaker, finalBreaker, finalRcd],
            [],
            [],
            new SingleLineInputMetadata(
                "1",
                "PLAYGROUND",
                "nested"),
            [serviceEntrance]);
    }

    private static string ResolveProfileDirectory()
    {
        string relativePath =
            Path.Combine(
                "data",
                "ric18",
                "v1");

        string currentDirectoryCandidate =
            Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    relativePath));

        if (Directory.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate =
                Path.Combine(
                    directory.FullName,
                    relativePath);

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository drawing profile data/ric18/v1.");
    }
}
