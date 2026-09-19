using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Layout;

public sealed record MoveEntityCommand(
    EntityUid EntityUid,
    MmPoint Position) : ILayoutCommand;
