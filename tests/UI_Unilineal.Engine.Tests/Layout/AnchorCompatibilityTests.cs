using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class AnchorCompatibilityTests
{
    [Fact]
    public void CompatibilityMatrix_IsExhaustiveAndMatchesContract()
    {
        AnchorRole[] roles = Enum.GetValues<AnchorRole>();

        foreach (AnchorRole source in roles)
        {
            foreach (AnchorRole target in roles)
            {
                bool expected =
                    (source == AnchorRole.PowerOut &&
                     target == AnchorRole.PowerIn) ||
                    (source == AnchorRole.BusTap &&
                     target == AnchorRole.PowerIn) ||
                    (source == AnchorRole.Ground &&
                     target == AnchorRole.Ground) ||
                    (source == AnchorRole.Reference &&
                     target == AnchorRole.Reference) ||
                    (source == AnchorRole.Annotation &&
                     target == AnchorRole.Annotation);

                Assert.Equal(
                    expected,
                    AnchorCompatibility.CanConnect(source, target));
            }
        }
    }
}
