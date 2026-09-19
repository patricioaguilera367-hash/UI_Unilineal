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
}
