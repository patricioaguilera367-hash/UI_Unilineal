namespace UI_Unilineal.Domain.Semantics;

public sealed class SingleLineInput
{
    public SingleLineInput(
        ProjectInput project,
        IEnumerable<SourceInput> sources,
        IEnumerable<BoardInput> boards,
        IEnumerable<BusInput> buses,
        IEnumerable<CircuitInput> circuits,
        IEnumerable<SupplyConnection> supplyConnections,
        IEnumerable<ProtectionInput> protections,
        IEnumerable<GroundingInput> grounding,
        IEnumerable<ElectricalResultInput> results,
        SingleLineInputMetadata metadata)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Sources = Copy(sources, nameof(sources));
        Boards = Copy(boards, nameof(boards));
        Buses = Copy(buses, nameof(buses));
        Circuits = Copy(circuits, nameof(circuits));
        SupplyConnections = Copy(supplyConnections, nameof(supplyConnections));
        Protections = Copy(protections, nameof(protections));
        Grounding = Copy(grounding, nameof(grounding));
        Results = Copy(results, nameof(results));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public ProjectInput Project { get; }
    public IReadOnlyList<SourceInput> Sources { get; }
    public IReadOnlyList<BoardInput> Boards { get; }
    public IReadOnlyList<BusInput> Buses { get; }
    public IReadOnlyList<CircuitInput> Circuits { get; }
    public IReadOnlyList<SupplyConnection> SupplyConnections { get; }
    public IReadOnlyList<ProtectionInput> Protections { get; }
    public IReadOnlyList<GroundingInput> Grounding { get; }
    public IReadOnlyList<ElectricalResultInput> Results { get; }
    public SingleLineInputMetadata Metadata { get; }

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }
}
