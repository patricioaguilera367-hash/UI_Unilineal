using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Validation;

internal sealed class SemanticEntityIndex
{
    private readonly HashSet<EntityUid> _projects = [];
    private readonly Dictionary<EntityUid, SourceInput> _sources = [];
    private readonly Dictionary<EntityUid, BoardInput> _boards = [];
    private readonly Dictionary<EntityUid, BusInput> _buses = [];
    private readonly Dictionary<EntityUid, CircuitInput> _circuits = [];
    private readonly HashSet<EntityUid> _supplyConnections = [];
    private readonly HashSet<EntityUid> _protections = [];
    private readonly HashSet<EntityUid> _grounding = [];
    private readonly List<EntityReference> _duplicates = [];

    public SemanticEntityIndex(SingleLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        AddScalar(input.Project.Uid, EntityKind.Project, _projects);

        foreach (SourceInput source in input.Sources)
        {
            Add(source.Uid, EntityKind.Source, _sources, source);
        }

        foreach (BoardInput board in input.Boards)
        {
            Add(board.Uid, EntityKind.Board, _boards, board);
        }

        foreach (BusInput bus in input.Buses)
        {
            Add(bus.Uid, EntityKind.Bus, _buses, bus);
        }

        foreach (CircuitInput circuit in input.Circuits)
        {
            Add(circuit.Uid, EntityKind.Circuit, _circuits, circuit);
        }

        foreach (SupplyConnection supply in input.SupplyConnections)
        {
            AddScalar(supply.Uid, EntityKind.SupplyConnection, _supplyConnections);
        }

        foreach (ProtectionInput protection in input.Protections)
        {
            AddScalar(protection.Uid, EntityKind.Protection, _protections);
        }

        foreach (GroundingInput grounding in input.Grounding)
        {
            AddScalar(grounding.Uid, EntityKind.Grounding, _grounding);
        }
    }

    public IReadOnlyList<EntityReference> Duplicates => _duplicates;

    public bool Exists(EntityReference reference) =>
        reference.Kind switch
        {
            EntityKind.Project => _projects.Contains(reference.Uid),
            EntityKind.Source => _sources.ContainsKey(reference.Uid),
            EntityKind.Board => _boards.ContainsKey(reference.Uid),
            EntityKind.Circuit => _circuits.ContainsKey(reference.Uid),
            EntityKind.SupplyConnection => _supplyConnections.Contains(reference.Uid),
            EntityKind.Protection => _protections.Contains(reference.Uid),
            EntityKind.Bus => _buses.ContainsKey(reference.Uid),
            EntityKind.Grounding => _grounding.Contains(reference.Uid),
            EntityKind.Load => false,
            _ => false
        };

    public bool TryGetBoard(EntityUid uid, out BoardInput? board) =>
        _boards.TryGetValue(uid, out board);

    public bool TryGetCircuit(EntityUid uid, out CircuitInput? circuit) =>
        _circuits.TryGetValue(uid, out circuit);

    public bool TryGetSource(EntityUid uid, out SourceInput? source) =>
        _sources.TryGetValue(uid, out source);

    public bool TryGetBus(EntityUid uid, out BusInput? bus) =>
        _buses.TryGetValue(uid, out bus);

    private void Add<T>(
        EntityUid uid,
        EntityKind kind,
        Dictionary<EntityUid, T> dictionary,
        T value)
    {
        if (!dictionary.TryAdd(uid, value))
        {
            _duplicates.Add(new EntityReference(uid, kind));
        }
    }

    private void AddScalar(
        EntityUid uid,
        EntityKind kind,
        HashSet<EntityUid>? values)
    {
        if (values is null)
        {
            return;
        }

        if (!values.Add(uid))
        {
            _duplicates.Add(new EntityReference(uid, kind));
        }
    }
}
