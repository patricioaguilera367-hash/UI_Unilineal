namespace UI_Unilineal.Engine.Documents;

public enum PaperPreset
{
    A0,
    A1,
    A2,
    A3,
    A4,
    Custom
}

public enum PageOrientation
{
    Portrait,
    Landscape
}

public sealed class PaperSize
{
    private PaperSize(
        PaperPreset preset,
        PageOrientation orientation,
        double widthMm,
        double heightMm)
    {
        ValidatePositive(widthMm, nameof(widthMm));
        ValidatePositive(heightMm, nameof(heightMm));

        Preset = preset;
        Orientation = orientation;
        WidthMm = widthMm;
        HeightMm = heightMm;
    }

    public PaperPreset Preset { get; }

    public PageOrientation Orientation { get; }

    public double WidthMm { get; }

    public double HeightMm { get; }

    public static PaperSize FromPreset(
        PaperPreset preset,
        PageOrientation orientation)
    {
        (double width, double height) = preset switch
        {
            PaperPreset.A0 => (841d, 1189d),
            PaperPreset.A1 => (594d, 841d),
            PaperPreset.A2 => (420d, 594d),
            PaperPreset.A3 => (297d, 420d),
            PaperPreset.A4 => (210d, 297d),
            PaperPreset.Custom => throw new ArgumentException(
                "Custom paper requires explicit dimensions.",
                nameof(preset)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };

        return orientation == PageOrientation.Landscape
            ? new PaperSize(preset, orientation, height, width)
            : new PaperSize(preset, orientation, width, height);
    }

    public static PaperSize Custom(
        double widthMm,
        double heightMm,
        PageOrientation orientation) =>
        new(PaperPreset.Custom, orientation, widthMm, heightMm);

    private static void ValidatePositive(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public readonly record struct SheetMargins
{
    public SheetMargins(
        double leftMm,
        double topMm,
        double rightMm,
        double bottomMm)
    {
        Validate(leftMm, nameof(leftMm));
        Validate(topMm, nameof(topMm));
        Validate(rightMm, nameof(rightMm));
        Validate(bottomMm, nameof(bottomMm));

        LeftMm = leftMm;
        TopMm = topMm;
        RightMm = rightMm;
        BottomMm = bottomMm;
    }

    public double LeftMm { get; }

    public double TopMm { get; }

    public double RightMm { get; }

    public double BottomMm { get; }

    private static void Validate(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
