using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Interaction.Layout;

public sealed record LayoutCommandResult(
    DiagramLayoutState State,
    DiagramLayoutState PreviousState,
    bool Changed);
