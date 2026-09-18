namespace UI_Unilineal.Domain.Semantics;

public enum OperationalState
{
    Active,
    Inactive,
    Unknown
}

public enum DataState
{
    Complete,
    Incomplete,
    Invalid,
    Unknown
}

public enum ResultState
{
    Current,
    Stale,
    Pending,
    Missing,
    Unknown
}
