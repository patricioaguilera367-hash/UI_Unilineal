using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Rendering.Avalonia.HitTesting;

public enum HitKind
{
    Anchor,
    Symbol,
    SemanticText,
    Branch,
    Connection,
    Group,
    Background
}

public sealed record HitTestResult(
    SceneId SceneElementId,
    EntityReference? Entity,
    HitKind Kind,
    string? AnchorId,
    double DistanceMm,
    int Priority);
