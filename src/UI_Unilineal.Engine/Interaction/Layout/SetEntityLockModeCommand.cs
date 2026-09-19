using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Layout;

public sealed record SetEntityLockModeCommand(
    EntityUid EntityUid,
    LayoutLockMode LockMode) : ILayoutCommand;
