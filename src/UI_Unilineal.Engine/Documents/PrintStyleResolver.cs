using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Documents;

public sealed class PrintStyleResolver
{
    public ResolvedDrawingStyleSet Resolve(RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        ResolvedLineStyle[] lines = profile.LineStyles
            .OrderBy(style => style.Id, StringComparer.Ordinal)
            .Select(style => new ResolvedLineStyle(
                style.Id,
                style.Role,
                style.WidthMm,
                style.Pattern))
            .ToArray();

        ResolvedTextStyle[] texts = profile.TextStyles
            .OrderBy(style => style.Id, StringComparer.Ordinal)
            .Select(style => new ResolvedTextStyle(
                style.Id,
                style.FontFamily,
                style.HeightMm,
                style.Bold))
            .ToArray();

        return new ResolvedDrawingStyleSet(
            profile.ProfileId,
            profile.Version,
            DrawingProfileFingerprint.Compute(profile),
            lines,
            texts);
    }
}
