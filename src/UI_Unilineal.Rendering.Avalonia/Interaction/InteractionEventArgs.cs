using UI_Unilineal.Engine.Interaction;

namespace UI_Unilineal.Rendering.Avalonia.Interaction;

public sealed class SelectionChangedEventArgs : EventArgs
{
    public SelectionChangedEventArgs(
        SelectionIntent intent)
    {
        Intent = intent ??
            throw new ArgumentNullException(
                nameof(intent));
    }

    public SelectionIntent Intent { get; }
}

public sealed class LayoutMoveRequestedEventArgs : EventArgs
{
    public LayoutMoveRequestedEventArgs(
        LayoutMoveIntent intent)
    {
        Intent = intent ??
            throw new ArgumentNullException(
                nameof(intent));
    }

    public LayoutMoveIntent Intent { get; }
}

public sealed class ElectricalProposalRequestedEventArgs : EventArgs
{
    public ElectricalProposalRequestedEventArgs(
        ElectricalConnectionIntent intent)
    {
        Intent = intent ??
            throw new ArgumentNullException(
                nameof(intent));
    }

    public ElectricalConnectionIntent Intent { get; }
}

public sealed class InteractionStateChangedEventArgs : EventArgs
{
    public InteractionStateChangedEventArgs(
        InteractionState previous,
        InteractionState current)
    {
        Previous = previous ??
            throw new ArgumentNullException(
                nameof(previous));
        Current = current ??
            throw new ArgumentNullException(
                nameof(current));
    }

    public InteractionState Previous { get; }

    public InteractionState Current { get; }
}
