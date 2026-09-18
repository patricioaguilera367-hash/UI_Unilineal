using UI_Unilineal.Domain.Profiles;

namespace UI_Unilineal.Engine.Layout;

public readonly record struct TextMeasurement
{
    public TextMeasurement(double widthMm, double heightMm)
    {
        if (!double.IsFinite(widthMm) || widthMm < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthMm));
        }

        if (!double.IsFinite(heightMm) || heightMm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightMm));
        }

        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public double WidthMm { get; }

    public double HeightMm { get; }
}

public interface ITextMetrics
{
    TextMeasurement Measure(string text, TextStyleDefinition style);
}
