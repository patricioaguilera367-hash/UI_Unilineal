namespace UI_Unilineal.Domain.Semantics;

public sealed record ProjectInput(
    EntityUid Uid,
    string Code,
    string Name,
    OperationalState State);
