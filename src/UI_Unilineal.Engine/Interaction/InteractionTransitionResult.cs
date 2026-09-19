namespace UI_Unilineal.Engine.Interaction;

public sealed record InteractionTransitionResult(
    bool Accepted,
    InteractionState Previous,
    InteractionState Current,
    string? RejectionReason)
{
    public static InteractionTransitionResult Applied(
        InteractionState previous,
        InteractionState current) =>
        new(
            Accepted: true,
            previous,
            current,
            RejectionReason: null);

    public static InteractionTransitionResult Rejected(
        InteractionState state,
        string reason) =>
        new(
            Accepted: false,
            state,
            state,
            reason);
}
