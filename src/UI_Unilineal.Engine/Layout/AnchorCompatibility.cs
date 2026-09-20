using UI_Unilineal.Domain.Connections;

namespace UI_Unilineal.Engine.Layout;

public static class AnchorCompatibility
{
    public static bool CanConnect(
        AnchorRole source,
        AnchorRole target) =>
        (source, target) switch
        {
            (AnchorRole.PowerOut, AnchorRole.PowerIn) => true,
            (AnchorRole.BusTap, AnchorRole.PowerIn) => true,
            (AnchorRole.Neutral, AnchorRole.Neutral) => true,
            (AnchorRole.Ground, AnchorRole.Ground) => true,
            (AnchorRole.Reference, AnchorRole.Reference) => true,
            (AnchorRole.Annotation, AnchorRole.Annotation) => true,
            _ => false
        };
}
