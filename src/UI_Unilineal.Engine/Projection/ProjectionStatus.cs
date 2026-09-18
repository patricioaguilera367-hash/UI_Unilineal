namespace UI_Unilineal.Engine.Projection;

public enum ProjectionStatus
{
    Ok,
    Warning,
    Error,
    Pending,
    Stale,
    Unknown
}

public enum BranchKind
{
    FinalCircuit,
    DownstreamBoard,
    Spare,
    Unknown
}

public enum DestinationKind
{
    Load,
    DownstreamBoard,
    External,
    Unknown
}
