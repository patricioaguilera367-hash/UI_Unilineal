using System.Text;
using UI_Unilineal.Domain.Profiles;

namespace UI_Unilineal.Engine.Layout;

public sealed class DeterministicTextMetrics : ITextMetrics
{
    public TextMeasurement Measure(string text, TextStyleDefinition style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);

        double units = 0;

        foreach (Rune rune in text.EnumerateRunes())
        {
            units += RelativeAdvance(rune);
        }

        double boldFactor = style.Bold ? 1.08 : 1.0;
        double width = units * style.HeightMm * boldFactor;

        return new TextMeasurement(width, style.HeightMm);
    }

    private static double RelativeAdvance(Rune rune)
    {
        if (Rune.IsWhiteSpace(rune))
        {
            return 0.33;
        }

        if (rune.Value <= 0x7F)
        {
            char value = (char)rune.Value;

            if (char.IsUpper(value))
            {
                return 0.62;
            }

            if (char.IsLower(value) || char.IsDigit(value))
            {
                return 0.55;
            }

            return 0.38;
        }

        return 0.70;
    }
}
