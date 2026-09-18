using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Validation;

internal sealed class SupplyTopologyValidator
{
    public IEnumerable<ValidationIssue> Validate(
        SingleLineInput input,
        SemanticEntityIndex index)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(index);

        var issues = new List<ValidationIssue>();

        ValidateSupplyRules(input, index, issues);
        ValidateMainBuses(input, issues);
        ValidateActiveCycles(input, index, issues);

        return issues;
    }

    private static void ValidateSupplyRules(
        SingleLineInput input,
        SemanticEntityIndex index,
        ICollection<ValidationIssue> issues)
    {
        foreach (SupplyConnection supply in input.SupplyConnections)
        {
            if (supply.Origin.Kind == EntityKind.Board)
            {
                if (supply.Origin.Uid == supply.DestinationBoardUid)
                {
                    issues.Add(new ValidationIssue(
                        ValidationCodes.SelfSupply,
                        ValidationSeverity.Error,
                        $"Board '{supply.Origin.Uid}' cannot supply itself.",
                        new EntityReference(supply.Uid, EntityKind.SupplyConnection)));
                }

                if (supply.ThroughCircuitUid is null)
                {
                    issues.Add(new ValidationIssue(
                        ValidationCodes.BoardSupplyRequiresCircuit,
                        ValidationSeverity.Error,
                        $"Board-origin supply '{supply.Uid}' requires a through circuit.",
                        new EntityReference(supply.Uid, EntityKind.SupplyConnection),
                        nameof(SupplyConnection.ThroughCircuitUid)));
                }
                else if (index.TryGetCircuit(supply.ThroughCircuitUid, out CircuitInput? circuit) &&
                         circuit is not null &&
                         circuit.BoardUid != supply.Origin.Uid)
                {
                    issues.Add(new ValidationIssue(
                        ValidationCodes.SupplyCircuitOwnerMismatch,
                        ValidationSeverity.Error,
                        $"Supply circuit '{circuit.Uid}' is not owned by origin board '{supply.Origin.Uid}'.",
                        new EntityReference(supply.Uid, EntityKind.SupplyConnection),
                        nameof(SupplyConnection.ThroughCircuitUid)));
                }
            }
            else if (supply.Origin.Kind == EntityKind.Source &&
                     supply.ThroughCircuitUid is not null)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.SourceSupplyCannotUseBoardCircuit,
                    ValidationSeverity.Error,
                    $"Source-origin supply '{supply.Uid}' cannot use a board circuit.",
                    new EntityReference(supply.Uid, EntityKind.SupplyConnection),
                    nameof(SupplyConnection.ThroughCircuitUid)));
            }
        }
    }

    private static void ValidateMainBuses(
        SingleLineInput input,
        ICollection<ValidationIssue> issues)
    {
        IEnumerable<IGrouping<EntityUid, BusInput>> duplicateMainBuses = input.Buses
            .Where(bus => bus.Role == BusRole.Main)
            .GroupBy(bus => bus.BoardUid)
            .Where(group => group.Count() > 1);

        foreach (IGrouping<EntityUid, BusInput> group in duplicateMainBuses)
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.MultipleMainBuses,
                ValidationSeverity.Error,
                $"Board '{group.Key}' has more than one main bus.",
                new EntityReference(group.Key, EntityKind.Board)));
        }
    }

    private static void ValidateActiveCycles(
        SingleLineInput input,
        SemanticEntityIndex index,
        ICollection<ValidationIssue> issues)
    {
        Dictionary<EntityUid, IReadOnlyList<EntityUid>> adjacency = BuildAdjacency(input, index);
        var states = new Dictionary<EntityUid, VisitState>();
        var reported = new HashSet<EntityUid>();

        foreach (BoardInput board in input.Boards
                     .OrderBy(x => x.Number)
                     .ThenBy(x => x.Code, StringComparer.Ordinal)
                     .ThenBy(x => x.Uid.Value, StringComparer.Ordinal))
        {
            if (GetState(states, board.Uid) == VisitState.Unvisited)
            {
                Visit(board.Uid, adjacency, states, reported, issues);
            }
        }
    }

    private static Dictionary<EntityUid, IReadOnlyList<EntityUid>> BuildAdjacency(
        SingleLineInput input,
        SemanticEntityIndex index)
    {
        var raw = new Dictionary<EntityUid, List<EntityUid>>();

        foreach (SupplyConnection supply in input.SupplyConnections)
        {
            if (supply.Origin.Kind != EntityKind.Board ||
                supply.State != OperationalState.Active ||
                !supply.IsNormallyActive ||
                supply.Origin.Uid == supply.DestinationBoardUid ||
                !index.TryGetBoard(supply.Origin.Uid, out _) ||
                !index.TryGetBoard(supply.DestinationBoardUid, out _))
            {
                continue;
            }

            if (!raw.TryGetValue(supply.Origin.Uid, out List<EntityUid>? destinations))
            {
                destinations = [];
                raw.Add(supply.Origin.Uid, destinations);
            }

            destinations.Add(supply.DestinationBoardUid);
        }

        return raw.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<EntityUid>)pair.Value
                .Distinct()
                .OrderBy(uid => uid.Value, StringComparer.Ordinal)
                .ToArray());
    }

    private static void Visit(
        EntityUid current,
        IReadOnlyDictionary<EntityUid, IReadOnlyList<EntityUid>> adjacency,
        IDictionary<EntityUid, VisitState> states,
        ISet<EntityUid> reported,
        ICollection<ValidationIssue> issues)
    {
        states[current] = VisitState.Visiting;

        if (adjacency.TryGetValue(current, out IReadOnlyList<EntityUid>? destinations))
        {
            foreach (EntityUid destination in destinations)
            {
                VisitState state = GetState(states, destination);
                if (state == VisitState.Unvisited)
                {
                    Visit(destination, adjacency, states, reported, issues);
                }
                else if (state == VisitState.Visiting && reported.Add(destination))
                {
                    issues.Add(new ValidationIssue(
                        ValidationCodes.SupplyCycle,
                        ValidationSeverity.Error,
                        $"Normally-active board supply graph contains a cycle involving '{destination}'.",
                        new EntityReference(destination, EntityKind.Board)));
                }
            }
        }

        states[current] = VisitState.Visited;
    }

    private static VisitState GetState(
        IReadOnlyDictionary<EntityUid, VisitState> states,
        EntityUid uid) =>
        states.TryGetValue(uid, out VisitState state)
            ? state
            : VisitState.Unvisited;

    private enum VisitState
    {
        Unvisited,
        Visiting,
        Visited
    }
}
