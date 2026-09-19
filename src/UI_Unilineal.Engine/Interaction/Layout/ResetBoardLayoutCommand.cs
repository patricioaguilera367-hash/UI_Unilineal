using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Layout;

public sealed record ResetBoardLayoutCommand(
    EntityUid BoardUid) : ILayoutCommand;
