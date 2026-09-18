using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Projection;

internal sealed class BoardDetailProjectionBuilder
{
    public BoardDetailProjection Build(
        BoardInput board,
        SingleLineInput input,
        SemanticEntityIndex index,
        IReadOnlyList<ProjectionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(issues);

        IncomingSupplyProjection[] incoming = input.SupplyConnections
            .Where(supply => supply.DestinationBoardUid == board.Uid)
            .OrderBy(supply => supply.Priority)
            .ThenBy(supply => supply.Role)
            .ThenBy(supply => supply.Uid.Value, StringComparer.Ordinal)
            .Select(supply => BuildIncomingSupply(supply, index, issues))
            .ToArray();

        BusInput mainBusInput = ResolveMainBus(board, input);
        BusProjection mainBus = BuildMainBus(board, mainBusInput, input, issues);

        BranchProjection[] branches = input.Circuits
            .Where(circuit => circuit.BoardUid == board.Uid)
            .OrderBy(circuit => circuit.Number)
            .ThenBy(circuit => circuit.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(circuit => circuit.Uid.Value, StringComparer.Ordinal)
            .Select(circuit => BuildBranch(board, circuit, input, index, issues))
            .ToArray();

        GroundingInput[] grounding = input.Grounding
            .Where(item => item.Owner == new EntityReference(board.Uid, EntityKind.Board))
            .OrderBy(item => item.Uid.Value, StringComparer.Ordinal)
            .ToArray();

        ProjectionIssue[] boardIssues = IssuesFor(
            new EntityReference(board.Uid, EntityKind.Board),
            issues);

        EntityReference[] navigationTargets = branches
            .Select(branch => branch.Destination.NavigationTarget)
            .Where(target => target is not null)
            .Cast<EntityReference>()
            .Distinct()
            .ToArray();

        return new BoardDetailProjection(
            board,
            incoming,
            mainBus,
            branches,
            grounding,
            boardIssues,
            navigationTargets);
    }

    private static IncomingSupplyProjection BuildIncomingSupply(
        SupplyConnection supply,
        SemanticEntityIndex index,
        IReadOnlyList<ProjectionIssue> issues)
    {
        SourceInput? source = null;
        BoardInput? originBoard = null;
        CircuitInput? throughCircuit = null;

        if (supply.Origin.Kind == EntityKind.Source)
        {
            index.TryGetSource(supply.Origin.Uid, out source);
        }
        else if (supply.Origin.Kind == EntityKind.Board)
        {
            index.TryGetBoard(supply.Origin.Uid, out originBoard);
        }

        if (supply.ThroughCircuitUid is not null)
        {
            index.TryGetCircuit(supply.ThroughCircuitUid, out throughCircuit);
        }

        ProjectionIssue[] supplyIssues = IssuesFor(
            new EntityReference(supply.Uid, EntityKind.SupplyConnection),
            issues);

        return new IncomingSupplyProjection(
            supply,
            source,
            originBoard,
            throughCircuit,
            ProjectionStatusResolver.Resolve(supply.DataState, null, supplyIssues));
    }

    private static BusInput ResolveMainBus(
        BoardInput board,
        SingleLineInput input)
    {
        BusInput? explicitMain = input.Buses
            .Where(bus => bus.BoardUid == board.Uid && bus.Role == BusRole.Main)
            .OrderBy(bus => bus.Uid.Value, StringComparer.Ordinal)
            .FirstOrDefault();

        if (explicitMain is not null)
        {
            return explicitMain;
        }

        return new BusInput(
            new EntityUid($"BUS:{board.Uid}:MAIN"),
            board.Uid,
            "MAIN",
            BusRole.Main,
            null,
            OperationalState.Active,
            DataState.Complete);
    }

    private static BusProjection BuildMainBus(
        BoardInput board,
        BusInput bus,
        SingleLineInput input,
        IReadOnlyList<ProjectionIssue> issues)
    {
        EntityReference busEntity = new(bus.Uid, EntityKind.Bus);
        EntityReference boardEntity = new(board.Uid, EntityKind.Board);

        ProtectionInput[] mainProtections = input.Protections
            .Where(protection =>
                protection.Protects == busEntity ||
                (protection.Role == ProtectionRole.Main && protection.Owner == boardEntity))
            .OrderBy(protection => ProtectionRoleOrder(protection.Role))
            .ThenBy(protection => protection.Uid.Value, StringComparer.Ordinal)
            .ToArray();

        ProjectionIssue[] busIssues = IssuesFor(busEntity, issues);

        return new BusProjection(
            bus,
            mainProtections,
            ProjectionStatusResolver.Resolve(bus.DataState, null, busIssues),
            busIssues.Length);
    }

    private static BranchProjection BuildBranch(
        BoardInput board,
        CircuitInput circuit,
        SingleLineInput input,
        SemanticEntityIndex index,
        IReadOnlyList<ProjectionIssue> issues)
    {
        EntityReference circuitEntity = new(circuit.Uid, EntityKind.Circuit);
        ProjectionIssue[] circuitIssues = IssuesFor(circuitEntity, issues);

        ProtectionInput[] protections = input.Protections
            .Where(protection => protection.Protects == circuitEntity)
            .OrderBy(protection => ProtectionRoleOrder(protection.Role))
            .ThenBy(protection => protection.Uid.Value, StringComparer.Ordinal)
            .ToArray();

        ElectricalResultInput? result = input.Results
            .Where(item => item.Entity == circuitEntity)
            .OrderBy(item => item.ExecutionReference, StringComparer.Ordinal)
            .FirstOrDefault();

        (BranchDestination destination, BranchKind kind) =
            ResolveDestination(board, circuit, input, index);

        ProjectionStatus status = ProjectionStatusResolver.Resolve(
            circuit.DataState,
            result?.ResultState,
            circuitIssues);

        return new BranchProjection(
            circuit,
            protections,
            result,
            destination,
            kind,
            status,
            circuitIssues);
    }

    private static (BranchDestination Destination, BranchKind Kind) ResolveDestination(
        BoardInput board,
        CircuitInput circuit,
        SingleLineInput input,
        SemanticEntityIndex index)
    {
        SupplyConnection[] destinations = input.SupplyConnections
            .Where(supply =>
                supply.Origin.Kind == EntityKind.Board &&
                supply.Origin.Uid == board.Uid &&
                supply.ThroughCircuitUid == circuit.Uid)
            .OrderBy(supply => supply.Priority)
            .ThenBy(supply => supply.Role)
            .ThenBy(supply => supply.Uid.Value, StringComparer.Ordinal)
            .ToArray();

        if (destinations.Length == 1 &&
            index.TryGetBoard(destinations[0].DestinationBoardUid, out BoardInput? downstream) &&
            downstream is not null)
        {
            var target = new EntityReference(downstream.Uid, EntityKind.Board);
            return (
                new BranchDestination(
                    DestinationKind.DownstreamBoard,
                    target,
                    downstream.Name,
                    target),
                BranchKind.DownstreamBoard);
        }

        if (destinations.Length > 1)
        {
            return (
                new BranchDestination(
                    DestinationKind.Unknown,
                    null,
                    circuit.Name,
                    null),
                BranchKind.Unknown);
        }

        if (circuit.Role == CircuitRole.Reserved)
        {
            return (
                new BranchDestination(
                    DestinationKind.Unknown,
                    null,
                    circuit.Name,
                    null),
                BranchKind.Spare);
        }

        return (
            new BranchDestination(
                DestinationKind.Load,
                null,
                circuit.Name,
                null),
            BranchKind.FinalCircuit);
    }

    private static int ProtectionRoleOrder(ProtectionRole role) =>
        role switch
        {
            ProtectionRole.Main => 0,
            ProtectionRole.Feeder => 1,
            ProtectionRole.Branch => 2,
            ProtectionRole.Adopted => 3,
            ProtectionRole.Recommended => 4,
            ProtectionRole.Backup => 5,
            ProtectionRole.Other => 6,
            _ => int.MaxValue
        };

    private static ProjectionIssue[] IssuesFor(
        EntityReference entity,
        IEnumerable<ProjectionIssue> issues) =>
        issues.Where(issue => issue.Entity == entity).ToArray();
}
