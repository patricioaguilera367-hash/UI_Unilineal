namespace UI_Unilineal.Engine.Interaction;

public sealed class InteractionStateMachine
{
    public InteractionStateMachine(
        InteractionMode initialMode = InteractionMode.Navigate)
    {
        State = Idle(initialMode);
    }

    public InteractionState State { get; private set; }

    public InteractionState SetMode(
        InteractionMode mode)
    {
        if (State.Mode == mode)
        {
            return State;
        }

        State = Idle(mode);
        return State;
    }

    public InteractionTransitionResult Apply(
        InteractionEvent interactionEvent)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        InteractionState previous = State;
        InteractionState? next =
            Resolve(
                previous,
                interactionEvent,
                out string? rejectionReason);

        if (next is null)
        {
            return InteractionTransitionResult.Rejected(
                previous,
                rejectionReason ??
                "The interaction transition is not allowed.");
        }

        State = next;
        return InteractionTransitionResult.Applied(
            previous,
            next);
    }

    private static InteractionState? Resolve(
        InteractionState state,
        InteractionEvent interactionEvent,
        out string? rejectionReason)
    {
        rejectionReason = null;

        if (interactionEvent.Kind == InteractionEventKind.Cancel)
        {
            if (state.Kind == InteractionStateKind.Idle)
            {
                rejectionReason =
                    "There is no active interaction to cancel.";
                return null;
            }

            return Idle(state.Mode);
        }

        return interactionEvent.Kind switch
        {
            InteractionEventKind.HoverEntered =>
                BeginHover(
                    state,
                    interactionEvent,
                    out rejectionReason),
            InteractionEventKind.HoverCleared =>
                EndSimpleState(
                    state,
                    InteractionStateKind.Hovering,
                    "Hover can only be cleared while hovering.",
                    out rejectionReason),
            InteractionEventKind.BeginSelection =>
                BeginElementState(
                    state,
                    interactionEvent,
                    InteractionStateKind.Selecting,
                    "Selection can only begin from Idle or Hovering.",
                    out rejectionReason),
            InteractionEventKind.EndSelection =>
                EndSimpleState(
                    state,
                    InteractionStateKind.Selecting,
                    "Selection can only end while selecting.",
                    out rejectionReason),
            InteractionEventKind.BeginPan =>
                BeginNonElementState(
                    state,
                    InteractionStateKind.Panning,
                    "Pan can only begin from Idle or Hovering.",
                    out rejectionReason),
            InteractionEventKind.EndPan =>
                EndSimpleState(
                    state,
                    InteractionStateKind.Panning,
                    "Pan can only end while panning.",
                    out rejectionReason),
            InteractionEventKind.BeginLayoutDrag =>
                BeginLayoutDrag(
                    state,
                    interactionEvent,
                    out rejectionReason),
            InteractionEventKind.EndLayoutDrag =>
                EndSimpleState(
                    state,
                    InteractionStateKind.DraggingLayout,
                    "Layout drag can only end while dragging layout.",
                    out rejectionReason),
            InteractionEventKind.BeginElectricalConnection =>
                BeginElectricalConnection(
                    state,
                    interactionEvent,
                    out rejectionReason),
            InteractionEventKind.EndElectricalConnection =>
                EndElectricalConnection(
                    state,
                    interactionEvent,
                    out rejectionReason),
            InteractionEventKind.BeginMarquee =>
                BeginNonElementState(
                    state,
                    InteractionStateKind.MarqueeSelecting,
                    "Marquee selection can only begin from Idle or Hovering.",
                    out rejectionReason),
            InteractionEventKind.EndMarquee =>
                EndSimpleState(
                    state,
                    InteractionStateKind.MarqueeSelecting,
                    "Marquee selection can only end while marquee selecting.",
                    out rejectionReason),
            InteractionEventKind.ShowCommandPreview =>
                ShowCommandPreview(
                    state,
                    out rejectionReason),
            InteractionEventKind.AcceptCommandPreview =>
                EndSimpleState(
                    state,
                    InteractionStateKind.CommandPreview,
                    "Command preview can only be accepted while previewing a command.",
                    out rejectionReason),
            _ => Reject(
                $"Unsupported interaction event '{interactionEvent.Kind}'.",
                out rejectionReason)
        };
    }

    private static InteractionState? BeginHover(
        InteractionState state,
        InteractionEvent interactionEvent,
        out string? rejectionReason)
    {
        if (!CanBeginFromStableState(state))
        {
            return Reject(
                "Hover can only begin from Idle or Hovering.",
                out rejectionReason);
        }

        if (interactionEvent.SceneElementId is null)
        {
            return Reject(
                "Hover requires a scene element.",
                out rejectionReason);
        }

        rejectionReason = null;
        return new InteractionState(
            state.Mode,
            InteractionStateKind.Hovering,
            interactionEvent.SceneElementId);
    }

    private static InteractionState? BeginElementState(
        InteractionState state,
        InteractionEvent interactionEvent,
        InteractionStateKind kind,
        string invalidStateReason,
        out string? rejectionReason)
    {
        if (!CanBeginFromStableState(state))
        {
            return Reject(
                invalidStateReason,
                out rejectionReason);
        }

        if (interactionEvent.SceneElementId is null)
        {
            return Reject(
                "The interaction requires a scene element.",
                out rejectionReason);
        }

        rejectionReason = null;
        return new InteractionState(
            state.Mode,
            kind,
            interactionEvent.SceneElementId);
    }

    private static InteractionState? BeginNonElementState(
        InteractionState state,
        InteractionStateKind kind,
        string invalidStateReason,
        out string? rejectionReason)
    {
        if (!CanBeginFromStableState(state))
        {
            return Reject(
                invalidStateReason,
                out rejectionReason);
        }

        rejectionReason = null;
        return new InteractionState(
            state.Mode,
            kind);
    }

    private static InteractionState? BeginLayoutDrag(
        InteractionState state,
        InteractionEvent interactionEvent,
        out string? rejectionReason)
    {
        if (state.Mode != InteractionMode.Layout)
        {
            return Reject(
                "Layout drag requires explicit Layout mode.",
                out rejectionReason);
        }

        return BeginElementState(
            state,
            interactionEvent,
            InteractionStateKind.DraggingLayout,
            "Layout drag can only begin from Idle or Hovering.",
            out rejectionReason);
    }

    private static InteractionState? BeginElectricalConnection(
        InteractionState state,
        InteractionEvent interactionEvent,
        out string? rejectionReason)
    {
        if (state.Mode != InteractionMode.Electrical)
        {
            return Reject(
                "Electrical connection requires explicit Electrical mode.",
                out rejectionReason);
        }

        if (!CanBeginFromStableState(state))
        {
            return Reject(
                "Electrical connection can only begin from Idle or Hovering.",
                out rejectionReason);
        }

        if (interactionEvent.SceneElementId is null ||
            string.IsNullOrWhiteSpace(interactionEvent.AnchorId))
        {
            return Reject(
                "Electrical connection requires a semantic scene anchor.",
                out rejectionReason);
        }

        rejectionReason = null;
        return new InteractionState(
            state.Mode,
            InteractionStateKind.ConnectingElectrical,
            interactionEvent.SceneElementId,
            interactionEvent.AnchorId);
    }

    private static InteractionState? EndElectricalConnection(
        InteractionState state,
        InteractionEvent interactionEvent,
        out string? rejectionReason)
    {
        if (state.Kind != InteractionStateKind.ConnectingElectrical)
        {
            return Reject(
                "Electrical connection can only end while connecting electrical anchors.",
                out rejectionReason);
        }

        if (interactionEvent.SceneElementId is null ||
            string.IsNullOrWhiteSpace(interactionEvent.AnchorId))
        {
            return Reject(
                "Completing an electrical connection requires a target semantic anchor.",
                out rejectionReason);
        }

        rejectionReason = null;
        return new InteractionState(
            state.Mode,
            InteractionStateKind.CommandPreview,
            interactionEvent.SceneElementId,
            interactionEvent.AnchorId);
    }

    private static InteractionState? ShowCommandPreview(
        InteractionState state,
        out string? rejectionReason)
    {
        if (state.Kind != InteractionStateKind.ConnectingElectrical)
        {
            return Reject(
                "Command preview requires a completed electrical connection intent.",
                out rejectionReason);
        }

        rejectionReason = null;
        return new InteractionState(
            state.Mode,
            InteractionStateKind.CommandPreview,
            state.ActiveSceneElementId,
            state.ActiveAnchorId);
    }

    private static InteractionState? EndSimpleState(
        InteractionState state,
        InteractionStateKind requiredKind,
        string invalidStateReason,
        out string? rejectionReason)
    {
        if (state.Kind != requiredKind)
        {
            return Reject(
                invalidStateReason,
                out rejectionReason);
        }

        rejectionReason = null;
        return Idle(state.Mode);
    }

    private static bool CanBeginFromStableState(
        InteractionState state) =>
        state.Kind is InteractionStateKind.Idle or
            InteractionStateKind.Hovering;

    private static InteractionState Idle(
        InteractionMode mode) =>
        new(
            mode,
            InteractionStateKind.Idle);

    private static InteractionState? Reject(
        string reason,
        out string? rejectionReason)
    {
        rejectionReason = reason;
        return null;
    }
}
