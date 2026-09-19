using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Interaction;

namespace UI_Unilineal.Engine.Tests.Interaction;

public sealed class InteractionStateMachineTests
{
    private static readonly SceneId ElementId =
        new("interaction/element");

    [Fact]
    public void InitialState_IsNavigateIdle()
    {
        var machine =
            new InteractionStateMachine();

        Assert.Equal(
            new InteractionState(
                InteractionMode.Navigate,
                InteractionStateKind.Idle),
            machine.State);
    }

    [Theory]
    [InlineData(InteractionMode.Navigate, InteractionEventKind.BeginLayoutDrag, false)]
    [InlineData(InteractionMode.Navigate, InteractionEventKind.BeginElectricalConnection, false)]
    [InlineData(InteractionMode.Layout, InteractionEventKind.BeginLayoutDrag, true)]
    [InlineData(InteractionMode.Layout, InteractionEventKind.BeginElectricalConnection, false)]
    [InlineData(InteractionMode.Electrical, InteractionEventKind.BeginLayoutDrag, false)]
    [InlineData(InteractionMode.Electrical, InteractionEventKind.BeginElectricalConnection, true)]
    public void BeginEditGestures_AreGatedByExplicitMode(
        InteractionMode mode,
        InteractionEventKind eventKind,
        bool expectedAccepted)
    {
        var machine =
            new InteractionStateMachine(mode);

        InteractionEvent interactionEvent =
            eventKind == InteractionEventKind.BeginElectricalConnection
                ? new InteractionEvent(
                    eventKind,
                    ElementId,
                    "anchor-out")
                : new InteractionEvent(
                    eventKind,
                    ElementId);

        InteractionTransitionResult result =
            machine.Apply(interactionEvent);

        Assert.Equal(
            expectedAccepted,
            result.Accepted);

        if (!expectedAccepted)
        {
            Assert.Equal(
                new InteractionState(
                    mode,
                    InteractionStateKind.Idle),
                machine.State);
        }
    }

    [Fact]
    public void ElectricalConnection_RequiresSemanticAnchor()
    {
        var machine =
            new InteractionStateMachine(
                InteractionMode.Electrical);

        InteractionTransitionResult result =
            machine.Apply(
                new InteractionEvent(
                    InteractionEventKind.BeginElectricalConnection,
                    ElementId));

        Assert.False(result.Accepted);
        Assert.Equal(
            InteractionStateKind.Idle,
            machine.State.Kind);
    }

    [Fact]
    public void SetMode_CancelsTransientState()
    {
        var machine =
            new InteractionStateMachine(
                InteractionMode.Layout);

        Assert.True(
            machine.Apply(
                new InteractionEvent(
                    InteractionEventKind.BeginLayoutDrag,
                    ElementId))
                .Accepted);
        Assert.Equal(
            InteractionStateKind.DraggingLayout,
            machine.State.Kind);

        InteractionState state =
            machine.SetMode(
                InteractionMode.Electrical);

        Assert.Equal(
            new InteractionState(
                InteractionMode.Electrical,
                InteractionStateKind.Idle),
            state);
        Assert.Equal(
            state,
            machine.State);
    }

    [Fact]
    public void Cancel_ReturnsToIdleWithoutChangingMode()
    {
        var machine =
            new InteractionStateMachine(
                InteractionMode.Electrical);

        Assert.True(
            machine.Apply(
                new InteractionEvent(
                    InteractionEventKind.BeginElectricalConnection,
                    ElementId,
                    "anchor-out"))
                .Accepted);

        InteractionTransitionResult cancelled =
            machine.Apply(
                new InteractionEvent(
                    InteractionEventKind.Cancel));

        Assert.True(cancelled.Accepted);
        Assert.Equal(
            new InteractionState(
                InteractionMode.Electrical,
                InteractionStateKind.Idle),
            machine.State);
    }

    [Fact]
    public void InvalidTransition_IsRejectedAndDoesNotMutateState()
    {
        var machine =
            new InteractionStateMachine(
                InteractionMode.Navigate);

        InteractionState before =
            machine.State;

        InteractionTransitionResult result =
            machine.Apply(
                new InteractionEvent(
                    InteractionEventKind.EndLayoutDrag,
                    ElementId));

        Assert.False(result.Accepted);
        Assert.Equal(
            before,
            result.Previous);
        Assert.Equal(
            before,
            result.Current);
        Assert.Equal(
            before,
            machine.State);
        Assert.False(
            string.IsNullOrWhiteSpace(
                result.RejectionReason));
    }
    [Fact]
    public void IdleTransitionMatrix_IsExhaustiveAcrossModesAndEvents()
    {
        foreach (InteractionMode mode in
                 Enum.GetValues<InteractionMode>())
        {
            foreach (InteractionEventKind eventKind in
                     Enum.GetValues<InteractionEventKind>())
            {
                var machine =
                    new InteractionStateMachine(mode);

                InteractionTransitionResult result =
                    machine.Apply(
                        EventFor(eventKind));
                InteractionStateKind? expected =
                    ExpectedFromIdle(
                        mode,
                        eventKind);

                Assert.Equal(
                    expected is not null,
                    result.Accepted);
                Assert.Equal(
                    expected ??
                    InteractionStateKind.Idle,
                    machine.State.Kind);
                Assert.Equal(
                    mode,
                    machine.State.Mode);
            }
        }
    }

    [Fact]
    public void CancelMatrix_CoversEveryReachableTransientState()
    {
        foreach (InteractionMode mode in
                 Enum.GetValues<InteractionMode>())
        {
            foreach (InteractionStateKind kind in
                     ReachableTransientKinds(mode))
            {
                InteractionStateMachine machine =
                    MachineInState(
                        mode,
                        kind);

                InteractionTransitionResult result =
                    machine.Apply(
                        new InteractionEvent(
                            InteractionEventKind.Cancel));

                Assert.True(result.Accepted);
                Assert.Equal(
                    new InteractionState(
                        mode,
                        InteractionStateKind.Idle),
                    machine.State);
            }
        }
    }

    private static InteractionEvent EventFor(
        InteractionEventKind kind) =>
        kind switch
        {
            InteractionEventKind.HoverEntered or
            InteractionEventKind.BeginSelection or
            InteractionEventKind.BeginLayoutDrag =>
                new InteractionEvent(
                    kind,
                    ElementId),
            InteractionEventKind.BeginElectricalConnection or
            InteractionEventKind.EndElectricalConnection =>
                new InteractionEvent(
                    kind,
                    ElementId,
                    "anchor"),
            _ =>
                new InteractionEvent(kind)
        };

    private static InteractionStateKind? ExpectedFromIdle(
        InteractionMode mode,
        InteractionEventKind kind) =>
        kind switch
        {
            InteractionEventKind.HoverEntered =>
                InteractionStateKind.Hovering,
            InteractionEventKind.BeginSelection =>
                InteractionStateKind.Selecting,
            InteractionEventKind.BeginPan =>
                InteractionStateKind.Panning,
            InteractionEventKind.BeginLayoutDrag
                when mode == InteractionMode.Layout =>
                InteractionStateKind.DraggingLayout,
            InteractionEventKind.BeginElectricalConnection
                when mode == InteractionMode.Electrical =>
                InteractionStateKind.ConnectingElectrical,
            InteractionEventKind.BeginMarquee =>
                InteractionStateKind.MarqueeSelecting,
            _ => null
        };

    private static IReadOnlyList<InteractionStateKind>
        ReachableTransientKinds(
            InteractionMode mode)
    {
        var kinds =
            new List<InteractionStateKind>
            {
                InteractionStateKind.Hovering,
                InteractionStateKind.Selecting,
                InteractionStateKind.Panning,
                InteractionStateKind.MarqueeSelecting
            };

        if (mode == InteractionMode.Layout)
        {
            kinds.Add(
                InteractionStateKind.DraggingLayout);
        }

        if (mode == InteractionMode.Electrical)
        {
            kinds.Add(
                InteractionStateKind.ConnectingElectrical);
            kinds.Add(
                InteractionStateKind.CommandPreview);
        }

        return kinds;
    }

    private static InteractionStateMachine MachineInState(
        InteractionMode mode,
        InteractionStateKind kind)
    {
        var machine =
            new InteractionStateMachine(mode);

        switch (kind)
        {
            case InteractionStateKind.Hovering:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.HoverEntered,
                        ElementId));
                break;

            case InteractionStateKind.Selecting:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.BeginSelection,
                        ElementId));
                break;

            case InteractionStateKind.Panning:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.BeginPan));
                break;

            case InteractionStateKind.MarqueeSelecting:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.BeginMarquee));
                break;

            case InteractionStateKind.DraggingLayout:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.BeginLayoutDrag,
                        ElementId));
                break;

            case InteractionStateKind.ConnectingElectrical:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.BeginElectricalConnection,
                        ElementId,
                        "anchor-out"));
                break;

            case InteractionStateKind.CommandPreview:
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.BeginElectricalConnection,
                        ElementId,
                        "anchor-out"));
                machine.Apply(
                    new InteractionEvent(
                        InteractionEventKind.EndElectricalConnection,
                        ElementId,
                        "anchor-in"));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(kind));
        }

        Assert.Equal(
            kind,
            machine.State.Kind);
        return machine;
    }

}
