namespace UI_Unilineal.Playground.Prototype;

public enum PrototypeBlockKind
{
    ServiceEntrance,
    MainBoard,
    DistributionBoard
}

public enum PrototypeInteractionMode
{
    Layout,
    Electrical
}

public interface IPrototypeCommand
{
    string Description { get; }
}

public sealed record LayoutMoveCommand(
    string EntityUid,
    double X,
    double Y) : IPrototypeCommand
{
    public string Description =>
        $"LayoutCommand · mover {EntityUid} a ({X:0}, {Y:0})";
}

public sealed record ElectricalChangeSupplyCommand(
    string BoardUid,
    string? PreviousParentUid,
    string NewParentUid) : IPrototypeCommand
{
    public string Description =>
        $"ElectricalCommand · alimentar {BoardUid}: " +
        $"{PreviousParentUid ?? "SIN_ORIGEN"} → {NewParentUid}";
}

public sealed class PrototypeBlock
{
    public required string Uid { get; init; }

    public required string Code { get; init; }

    public required string Title { get; init; }

    public required PrototypeBlockKind Kind { get; init; }

    public string? ParentUid { get; set; }

    public string FeederCode { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }
}

public sealed record PrototypeCircuitBranch(
    string Code,
    string Name,
    string Breaker,
    string Differential,
    string Conductor);

public sealed class PrototypeDiagramState
{
    private readonly List<PrototypeBlock> _blocks;

    private readonly Dictionary<string, IReadOnlyList<PrototypeCircuitBranch>> _circuits;

    private PrototypeDiagramState(
        IEnumerable<PrototypeBlock> blocks,
        Dictionary<string, IReadOnlyList<PrototypeCircuitBranch>> circuits)
    {
        _blocks = blocks.ToList();
        _circuits = circuits;
    }

    public IReadOnlyList<PrototypeBlock> Blocks => _blocks;

    public PrototypeBlock GetBlock(string uid)
    {
        return _blocks.Single(block => block.Uid == uid);
    }

    public IReadOnlyList<PrototypeCircuitBranch> GetCircuits(string boardUid)
    {
        return _circuits.TryGetValue(boardUid, out var circuits)
            ? circuits
            : Array.Empty<PrototypeCircuitBranch>();
    }

    public LayoutMoveCommand MoveBlock(
        string uid,
        double x,
        double y)
    {
        var block = GetBlock(uid);

        block.X = x;
        block.Y = y;

        return new LayoutMoveCommand(uid, x, y);
    }

    public ElectricalChangeSupplyCommand ChangeSupply(
        string boardUid,
        string newParentUid)
    {
        var board = GetBlock(boardUid);
        var previousParentUid = board.ParentUid;

        if (board.Kind == PrototypeBlockKind.ServiceEntrance)
        {
            throw new InvalidOperationException(
                "El empalme no puede ser alimentado por otro bloque.");
        }

        if (boardUid == newParentUid)
        {
            throw new InvalidOperationException(
                "Un tablero no puede alimentarse a si mismo.");
        }

        if (WouldCreateCycle(boardUid, newParentUid))
        {
            throw new InvalidOperationException(
                "La nueva alimentacion crearia un ciclo.");
        }

        board.ParentUid = newParentUid;

        return new ElectricalChangeSupplyCommand(
            boardUid,
            previousParentUid,
            newParentUid);
    }

    public bool WouldCreateCycle(
        string boardUid,
        string newParentUid)
    {
        string? cursor = newParentUid;

        while (cursor is not null)
        {
            if (cursor == boardUid)
            {
                return true;
            }

            var current = _blocks.SingleOrDefault(
                block => block.Uid == cursor);

            cursor = current?.ParentUid;
        }

        return false;
    }

    public static PrototypeDiagramState CreateDemo()
    {
        var blocks = new[]
        {
            new PrototypeBlock
            {
                Uid = "SOURCE:EMPALME-01",
                Code = "EMPALME",
                Title = "Empalme BT",
                Kind = PrototypeBlockKind.ServiceEntrance,
                ParentUid = null,
                FeederCode = string.Empty,
                X = 60,
                Y = 300
            },
            new PrototypeBlock
            {
                Uid = "BOARD:TGBT-01",
                Code = "TGBT-01",
                Title = "Tablero General",
                Kind = PrototypeBlockKind.MainBoard,
                ParentUid = "SOURCE:EMPALME-01",
                FeederCode = "AL-01",
                X = 300,
                Y = 300
            },
            new PrototypeBlock
            {
                Uid = "BOARD:TDA-01",
                Code = "TDA-01",
                Title = "Alumbrado 1",
                Kind = PrototypeBlockKind.DistributionBoard,
                ParentUid = "BOARD:TGBT-01",
                FeederCode = "C07",
                X = 590,
                Y = 90
            },
            new PrototypeBlock
            {
                Uid = "BOARD:TDF-01",
                Code = "TDF-01",
                Title = "Fuerza 1",
                Kind = PrototypeBlockKind.DistributionBoard,
                ParentUid = "BOARD:TGBT-01",
                FeederCode = "C08",
                X = 590,
                Y = 300
            },
            new PrototypeBlock
            {
                Uid = "BOARD:TD-CLIMA",
                Code = "TD-CLIMA",
                Title = "Climatizacion",
                Kind = PrototypeBlockKind.DistributionBoard,
                ParentUid = "BOARD:TGBT-01",
                FeederCode = "C09",
                X = 590,
                Y = 510
            },
            new PrototypeBlock
            {
                Uid = "BOARD:TDA-02",
                Code = "TDA-02",
                Title = "Alumbrado 2",
                Kind = PrototypeBlockKind.DistributionBoard,
                ParentUid = "BOARD:TDA-01",
                FeederCode = "C04",
                X = 890,
                Y = 90
            }
        };

        var circuits = new Dictionary<string, IReadOnlyList<PrototypeCircuitBranch>>
        {
            ["BOARD:TGBT-01"] = new[]
            {
                new PrototypeCircuitBranch(
                    "C07",
                    "Alimentador TDA-01",
                    "TM 3P 32 A · C",
                    "ID 4P 40 A · 30 mA",
                    "5 x 6 mm² Cu"),
                new PrototypeCircuitBranch(
                    "C08",
                    "Alimentador TDF-01",
                    "TM 3P 40 A · C",
                    "ID 4P 40 A · 30 mA",
                    "5 x 10 mm² Cu"),
                new PrototypeCircuitBranch(
                    "C09",
                    "Alimentador TD-CLIMA",
                    "TM 3P 50 A · C",
                    "ID 4P 63 A · 30 mA",
                    "5 x 16 mm² Cu")
            },
            ["BOARD:TDA-01"] = new[]
            {
                new PrototypeCircuitBranch(
                    "C01",
                    "Alumbrado oficinas",
                    "TM 1P 10 A · C",
                    "ID 2P 25 A · 30 mA",
                    "3 x 1,5 mm² Cu"),
                new PrototypeCircuitBranch(
                    "C02",
                    "Alumbrado pasillos",
                    "TM 1P 10 A · C",
                    "ID 2P 25 A · 30 mA",
                    "3 x 1,5 mm² Cu"),
                new PrototypeCircuitBranch(
                    "C03",
                    "Enchufes servicio",
                    "TM 1P 16 A · C",
                    "ID 2P 25 A · 30 mA",
                    "3 x 2,5 mm² Cu"),
                new PrototypeCircuitBranch(
                    "C04",
                    "Alimentador TDA-02",
                    "TM 3P 25 A · C",
                    "ID 4P 40 A · 30 mA",
                    "5 x 4 mm² Cu")
            },
            ["BOARD:TDF-01"] = new[]
            {
                new PrototypeCircuitBranch(
                    "C01",
                    "Motor bomba 1",
                    "TM 3P 20 A · D",
                    "ID 4P 25 A · 30 mA",
                    "5 x 4 mm² Cu"),
                new PrototypeCircuitBranch(
                    "C02",
                    "Motor bomba 2",
                    "TM 3P 20 A · D",
                    "ID 4P 25 A · 30 mA",
                    "5 x 4 mm² Cu")
            },
            ["BOARD:TD-CLIMA"] = new[]
            {
                new PrototypeCircuitBranch(
                    "C01",
                    "Unidad climatizacion 1",
                    "TM 3P 25 A · C",
                    "ID 4P 40 A · 30 mA",
                    "5 x 4 mm² Cu")
            },
            ["BOARD:TDA-02"] = new[]
            {
                new PrototypeCircuitBranch(
                    "C01",
                    "Alumbrado sector 2",
                    "TM 1P 10 A · C",
                    "ID 2P 25 A · 30 mA",
                    "3 x 1,5 mm² Cu")
            }
        };

        return new PrototypeDiagramState(blocks, circuits);
    }
}
