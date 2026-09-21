using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Domain.V2;

namespace UI_Unilineal.Playground.Fixtures;

public static class V2BoardDiagramFixtureFactory
{
    public static BoardDiagramModel Create()
    {
        return new BoardDiagramModel(
            new EntityUid("V2-B1"),
            "TGBT-01",
            "Tablero general — demo V2",
            "ACTIVO",
            0,
            null,
            [
                new BoardDiagramIncomingModel(
                    new EntityUid("V2-S1"),
                    new EntityUid("V2-FEED-N"),
                    new EntityUid("V2-ORIGIN-N"),
                    "EMPALME",
                    "Alimentación normal",
                    "NORMAL",
                    true,
                    "ACTIVO"),
                new BoardDiagramIncomingModel(
                    new EntityUid("V2-S2"),
                    new EntityUid("V2-FEED-A"),
                    new EntityUid("V2-ORIGIN-A"),
                    "ALT",
                    "Alimentación alternativa futura",
                    "ALTERNATIVA",
                    false,
                    "ACTIVO")
            ],
            [
                new BoardDiagramBranchModel(
                    new EntityUid("V2-C1"),
                    1,
                    "TGBT-01-C01",
                    "Alimentador múltiple",
                    "TRIFASICO",
                    "ACTIVO",
                    "ADOPTADO",
                    "3x40 A / Curva C",
                    false,
                    null,
                    "RZ1 10 mm²",
                    BoardDiagramPresence.Present,
                    "N 10 mm²",
                    BoardDiagramPresence.Unknown,
                    null,
                    [
                        Board(
                            "V2-B2",
                            "TDA-01",
                            "Alumbrado"),
                        Board(
                            "V2-B3",
                            "TDF-01",
                            "Fuerza"),
                        Board(
                            "V2-B4",
                            "TDS-01",
                            "Servicios")
                    ],
                    "NO_EVALUADO"),
                new BoardDiagramBranchModel(
                    new EntityUid("V2-C2"),
                    2,
                    "TGBT-01-C02",
                    "Circuito final",
                    "MONOFASICO",
                    "ACTIVO",
                    "ADOPTADO",
                    "1x16 A / Curva C",
                    true,
                    null,
                    "RZ1 2.5 mm²",
                    BoardDiagramPresence.Present,
                    "N 2.5 mm²",
                    BoardDiagramPresence.Unknown,
                    "Iluminación",
                    [],
                    "NO_EVALUADO"),
                new BoardDiagramBranchModel(
                    new EntityUid("V2-C3"),
                    3,
                    "TGBT-01-C03",
                    "Tablero dedicado",
                    "TRIFASICO",
                    "ACTIVO",
                    "ADOPTADO",
                    "3x25 A / Curva C",
                    false,
                    null,
                    "RZ1 6 mm²",
                    BoardDiagramPresence.Unknown,
                    null,
                    BoardDiagramPresence.Unknown,
                    null,
                    [
                        Board(
                            "V2-B5",
                            "TD-CLIMA",
                            "Climatización")
                    ],
                    "STALE")
            ]);
    }

    private static BoardDiagramTargetModel Board(
        string uid,
        string code,
        string name) =>
        new(
            new EntityUid(uid),
            EntityKind.Board,
            BoardDiagramTargetKind.DownstreamBoard,
            code,
            name);
}
