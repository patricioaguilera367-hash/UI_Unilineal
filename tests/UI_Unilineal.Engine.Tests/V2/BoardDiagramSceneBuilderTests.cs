using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Domain.V2;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.V2;

namespace UI_Unilineal.Engine.Tests.V2;

public sealed class BoardDiagramSceneBuilderTests
{
    [Fact]
    public void FanOutUsesExplicitJunctionBusAndNodes()
    {
        DiagramScene scene =
            Build(
                Model(
                    targets:
                    [
                        BoardTarget("B2", "TDA-01"),
                        BoardTarget("B3", "TDF-01"),
                        BoardTarget("B4", "TDS-01")
                    ]));

        LineSceneElement junction =
            Assert.Single(
                scene.Elements
                    .OfType<LineSceneElement>()
                    .Where(element =>
                        Role(element) ==
                        "JunctionBus"));

        CircleSceneElement[] nodes =
            scene.Elements
                .OfType<CircleSceneElement>()
                .Where(element =>
                    Role(element) ==
                    "JunctionBusNode")
                .OrderBy(element =>
                    element.Center.X)
                .ToArray();

        Assert.Equal(3, nodes.Length);
        Assert.All(
            nodes,
            node =>
                Assert.Equal(
                    junction.Start.Y,
                    node.Center.Y));

        LineSceneElement[] outgoing =
            scene.Elements
                .OfType<LineSceneElement>()
                .Where(element =>
                    element.Id.Value.Contains(
                        "/junction/out/",
                        StringComparison.Ordinal))
                .OrderBy(element =>
                    element.Start.X)
                .ToArray();

        Assert.Equal(3, outgoing.Length);
        Assert.All(
            outgoing,
            line =>
            {
                Assert.Equal(
                    junction.Start.Y,
                    line.Start.Y);
                Assert.Contains(
                    nodes,
                    node =>
                        node.Center ==
                        line.Start);
            });
    }

    [Fact]
    public void SingleDestinationDoesNotCreateJunctionBus()
    {
        DiagramScene scene =
            Build(
                Model(
                    targets:
                    [
                        BoardTarget(
                            "B2",
                            "TDA-01")
                    ]));

        Assert.DoesNotContain(
            scene.Elements,
            element =>
                Role(element) ==
                "JunctionBus");
        Assert.DoesNotContain(
            scene.Elements,
            element =>
                Role(element) ==
                "JunctionBusNode");

        Assert.Contains(
            scene.Elements,
            element =>
                element.Id.Value.Contains(
                    "/to-target",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void EquivalentReorderedInputProducesSameSceneFingerprint()
    {
        BoardDiagramModel first =
            Model(
                incoming:
                [
                    Incoming(
                        "S1",
                        "TGBT-A",
                        true,
                        "NORMAL"),
                    Incoming(
                        "S2",
                        "TGBT-B",
                        false,
                        "ALTERNATIVA")
                ],
                targets:
                [
                    BoardTarget(
                        "B2",
                        "TDA-01"),
                    BoardTarget(
                        "B3",
                        "TDF-01"),
                    BoardTarget(
                        "B4",
                        "TDS-01")
                ]);

        BoardDiagramModel second =
            Model(
                incoming:
                    first.IncomingSupplies
                        .Reverse()
                        .ToArray(),
                targets:
                    first.Branches[0].Targets
                        .Reverse()
                        .ToArray());

        DiagramScene firstScene =
            Build(first);
        DiagramScene secondScene =
            Build(second);

        Assert.Equal(
            DiagramSceneFingerprint.Compute(
                firstScene),
            DiagramSceneFingerprint.Compute(
                secondScene));
    }

    [Fact]
    public void AlternateIncomingSupplyIsDashedStyleAndEndsOnMainBusNode()
    {
        DiagramScene scene =
            Build(
                Model(
                    incoming:
                    [
                        Incoming(
                            "S1",
                            "TGBT-A",
                            true,
                            "NORMAL"),
                        Incoming(
                            "S2",
                            "TGBT-B",
                            false,
                            "ALTERNATIVA")
                    ]));

        LineSceneElement alternate =
            Assert.Single(
                scene.Elements
                    .OfType<LineSceneElement>()
                    .Where(element =>
                        element.LineStyleId ==
                        "ALTERNATE_SUPPLY"));

        CircleSceneElement[] mainNodes =
            scene.Elements
                .OfType<CircleSceneElement>()
                .Where(element =>
                    Role(element) ==
                    "MainBusNode")
                .ToArray();

        Assert.Contains(
            mainNodes,
            node =>
                node.Center ==
                alternate.End);
    }

    [Fact]
    public void UnknownNeutralAndPeAreNotInventedAsConductors()
    {
        DiagramScene scene =
            Build(
                Model());

        Assert.DoesNotContain(
            scene.Elements
                .OfType<LineSceneElement>(),
            element =>
                element.LineStyleId is
                    "NEUTRAL_AUX" or
                    "GROUND_AUX");

        Assert.Contains(
            scene.Issues,
            issue =>
                issue.Code ==
                "NEUTRAL_TOPOLOGY_UNKNOWN");
        Assert.Contains(
            scene.Issues,
            issue =>
                issue.Code ==
                "PE_TOPOLOGY_UNKNOWN");
    }

    [Fact]
    public void SceneUsesExpandedVectorPrimitivesForSupportedRicSymbols()
    {
        DiagramScene scene =
            Build(
                Model(
                    breakerLabel:
                        "3x40 A / Curva C",
                    targets:
                    [
                        BoardTarget(
                            "B2",
                            "TDA-01")
                    ]));

        Assert.Contains(
            scene.Elements,
            element =>
                element.Id.Value.Contains(
                    "/breaker/primitive/",
                    StringComparison.Ordinal));
        Assert.Contains(
            scene.Elements,
            element =>
                element.Id.Value.Contains(
                    "/target/01-B2/primitive/",
                    StringComparison.Ordinal));
        Assert.DoesNotContain(
            scene.Elements,
            element =>
                element is
                    SymbolSceneElement);
    }

    private static DiagramScene Build(
        BoardDiagramModel model)
    {
        RIC18DrawingProfile profile =
            new Ric18DrawingProfileLoader()
                .LoadDirectory(
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "ProfileData"));

        return new BoardDiagramSceneBuilder()
            .Build(
                model,
                profile);
    }

    private static BoardDiagramModel Model(
        IReadOnlyList<BoardDiagramIncomingModel>? incoming = null,
        IReadOnlyList<BoardDiagramTargetModel>? targets = null,
        string? breakerLabel = "3x40 A / Curva C")
    {
        var boardUid =
            new EntityUid("B1");
        var circuitUid =
            new EntityUid("C1");

        return new BoardDiagramModel(
            boardUid,
            "TGBT-01",
            "Tablero general",
            "ACTIVO",
            incoming ?? [],
            [
                new BoardDiagramBranchModel(
                    circuitUid,
                    1,
                    "TGBT-01-C01",
                    "Alimentador",
                    "TRIFASICO",
                    "ACTIVO",
                    "ADOPTADO",
                    breakerLabel,
                    null,
                    "RZ1 10 mm²",
                    BoardDiagramPresence.Unknown,
                    null,
                    BoardDiagramPresence.Unknown,
                    targets ??
                    [
                        new BoardDiagramTargetModel(
                            new EntityUid(
                                "LOAD-C1"),
                            EntityKind.Load,
                            BoardDiagramTargetKind.FinalLoad,
                            "C01",
                            "Carga final")
                    ],
                    "NO_EVALUADO")
            ]);
    }

    private static BoardDiagramIncomingModel Incoming(
        string uid,
        string originCode,
        bool normallyActive,
        string role) =>
        new(
            new EntityUid(uid),
            new EntityUid(
                $"FC-{uid}"),
            new EntityUid(
                $"OB-{uid}"),
            originCode,
            originCode,
            role,
            normallyActive,
            "ACTIVO");

    private static BoardDiagramTargetModel BoardTarget(
        string uid,
        string code) =>
        new(
            new EntityUid(uid),
            EntityKind.Board,
            BoardDiagramTargetKind.DownstreamBoard,
            code,
            code);

    private static string? Role(
        SceneElement element) =>
        element.Metadata.TryGetValue(
            "v2Role",
            out string? role)
            ? role
            : null;
}
