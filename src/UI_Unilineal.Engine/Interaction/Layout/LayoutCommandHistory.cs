using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Interaction.Layout;

public sealed class LayoutCommandHistory
{
    private readonly LayoutCommandReducer _reducer = new();
    private readonly Stack<HistoryEntry> _undo = new();
    private readonly Stack<HistoryEntry> _redo = new();

    public LayoutCommandHistory(
        DiagramLayoutState initialState)
    {
        CurrentState =
            initialState ??
            throw new ArgumentNullException(
                nameof(initialState));
    }

    public DiagramLayoutState CurrentState { get; private set; }

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public LayoutCommandResult Execute(
        ILayoutCommand command)
    {
        LayoutCommandResult result =
            _reducer.Apply(
                CurrentState,
                command);

        if (!result.Changed)
        {
            return result;
        }

        var entry =
            new HistoryEntry(
                result.PreviousState,
                result.State);

        _undo.Push(entry);
        _redo.Clear();
        CurrentState = result.State;

        return result;
    }

    public DiagramLayoutState Undo()
    {
        if (!CanUndo)
        {
            return CurrentState;
        }

        HistoryEntry entry =
            _undo.Pop();

        _redo.Push(entry);
        CurrentState = entry.Before;

        return CurrentState;
    }

    public DiagramLayoutState Redo()
    {
        if (!CanRedo)
        {
            return CurrentState;
        }

        HistoryEntry entry =
            _redo.Pop();

        _undo.Push(entry);
        CurrentState = entry.After;

        return CurrentState;
    }

    private sealed record HistoryEntry(
        DiagramLayoutState Before,
        DiagramLayoutState After);
}
