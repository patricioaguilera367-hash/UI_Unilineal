using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction.Layout;

namespace UI_Unilineal.Engine.Tests.Interaction;

public sealed class LayoutCommandHistoryTests
{
    private static readonly EntityUid BoardUid =
        new("board-1");

    private static readonly EntityUid CircuitA =
        new("circuit-a");

    private static readonly EntityUid CircuitB =
        new("circuit-b");

    [Fact]
    public void MoveEntity_CreatesPinnedOverrideAndCapturesExactPreviousState()
    {
        DiagramLayoutState initial =
            State();

        var reducer =
            new LayoutCommandReducer();

        LayoutCommandResult result =
            reducer.Apply(
                initial,
                new MoveEntityCommand(
                    CircuitA,
                    new MmPoint(120, 80)));

        Assert.True(result.Changed);
        Assert.Same(initial, result.PreviousState);

        LayoutOverride moved =
            Assert.Single(result.State.Overrides);

        Assert.Equal(CircuitA, moved.EntityUid);
        Assert.Equal(
            new MmPoint(120, 80),
            moved.Position);
        Assert.Equal(
            LayoutLockMode.Pinned,
            moved.LockMode);
    }

    [Fact]
    public void MoveEntity_PreservesExistingLockMode()
    {
        DiagramLayoutState initial =
            State(
                new LayoutOverride(
                    CircuitA,
                    new MmPoint(10, 20),
                    LayoutLockMode.Locked));

        LayoutCommandResult result =
            new LayoutCommandReducer().Apply(
                initial,
                new MoveEntityCommand(
                    CircuitA,
                    new MmPoint(30, 40)));

        LayoutOverride moved =
            Assert.Single(result.State.Overrides);

        Assert.Equal(
            LayoutLockMode.Locked,
            moved.LockMode);
        Assert.Equal(
            new MmPoint(30, 40),
            moved.Position);
    }

    [Fact]
    public void SetEntityLockMode_PreservesPosition()
    {
        DiagramLayoutState initial =
            State(
                new LayoutOverride(
                    CircuitA,
                    new MmPoint(15, 25),
                    LayoutLockMode.Pinned));

        LayoutCommandResult result =
            new LayoutCommandReducer().Apply(
                initial,
                new SetEntityLockModeCommand(
                    CircuitA,
                    LayoutLockMode.Locked));

        LayoutOverride updated =
            Assert.Single(result.State.Overrides);

        Assert.Equal(
            new MmPoint(15, 25),
            updated.Position);
        Assert.Equal(
            LayoutLockMode.Locked,
            updated.LockMode);
    }

    [Fact]
    public void ResetEntityPosition_RemovesOnlyTheTargetOverride()
    {
        DiagramLayoutState initial =
            State(
                new LayoutOverride(
                    CircuitA,
                    new MmPoint(10, 20),
                    LayoutLockMode.Pinned),
                new LayoutOverride(
                    CircuitB,
                    new MmPoint(30, 40),
                    LayoutLockMode.Locked));

        LayoutCommandResult result =
            new LayoutCommandReducer().Apply(
                initial,
                new ResetEntityPositionCommand(
                    CircuitA));

        LayoutOverride remaining =
            Assert.Single(result.State.Overrides);

        Assert.Equal(CircuitB, remaining.EntityUid);
    }

    [Fact]
    public void ResetBoardLayout_ClearsMatchingBoardOverridesAndPreservesViewport()
    {
        var viewport =
            new DiagramViewportPreference(
                1.5,
                new MmPoint(100, 200));
        DiagramLayoutState initial =
            State(
                viewport,
                new LayoutOverride(
                    CircuitA,
                    new MmPoint(10, 20),
                    LayoutLockMode.Pinned),
                new LayoutOverride(
                    CircuitB,
                    new MmPoint(30, 40),
                    LayoutLockMode.Locked));

        LayoutCommandResult result =
            new LayoutCommandReducer().Apply(
                initial,
                new ResetBoardLayoutCommand(
                    BoardUid));

        Assert.True(result.Changed);
        Assert.Empty(result.State.Overrides);
        Assert.Same(
            viewport,
            result.State.ViewportPreference);
    }

    [Fact]
    public void History_ExecuteUndoRedo_RoundTripsExactLayoutState()
    {
        DiagramLayoutState initial =
            State();
        var history =
            new LayoutCommandHistory(initial);

        history.Execute(
            new MoveEntityCommand(
                CircuitA,
                new MmPoint(10, 20)));
        DiagramLayoutState afterA =
            history.CurrentState;

        history.Execute(
            new MoveEntityCommand(
                CircuitB,
                new MmPoint(30, 40)));
        DiagramLayoutState afterB =
            history.CurrentState;

        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        Assert.Equal(
            Signature(afterA),
            Signature(history.Undo()));
        Assert.Equal(
            Signature(initial),
            Signature(history.Undo()));

        Assert.True(history.CanRedo);
        Assert.Equal(
            Signature(afterA),
            Signature(history.Redo()));
        Assert.Equal(
            Signature(afterB),
            Signature(history.Redo()));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void NoOpCommand_DoesNotPolluteHistory()
    {
        var history =
            new LayoutCommandHistory(
                State());

        LayoutCommandResult result =
            history.Execute(
                new ResetEntityPositionCommand(
                    CircuitA));

        Assert.False(result.Changed);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void ExecuteAfterUndo_ClearsRedoBranch()
    {
        var history =
            new LayoutCommandHistory(
                State());

        history.Execute(
            new MoveEntityCommand(
                CircuitA,
                new MmPoint(10, 20)));
        history.Execute(
            new MoveEntityCommand(
                CircuitB,
                new MmPoint(30, 40)));

        history.Undo();

        Assert.True(history.CanRedo);

        history.Execute(
            new MoveEntityCommand(
                CircuitB,
                new MmPoint(50, 60)));

        Assert.False(history.CanRedo);
    }

    private static DiagramLayoutState State(
        params LayoutOverride[] overrides) =>
        State(
            viewportPreference: null,
            overrides);

    private static DiagramLayoutState State(
        DiagramViewportPreference? viewportPreference,
        params LayoutOverride[] overrides) =>
        new(
            DiagramSceneKind.BoardDetail,
            BoardUid,
            "1",
            overrides,
            viewportPreference);

    private static string Signature(
        DiagramLayoutState state) =>
        string.Join(
            "|",
            state.Overrides
                .OrderBy(
                    item => item.EntityUid.Value,
                    StringComparer.Ordinal)
                .Select(
                    item =>
                        $"{item.EntityUid.Value}:{item.Position.X:R},{item.Position.Y:R}:{item.LockMode}"));
}
