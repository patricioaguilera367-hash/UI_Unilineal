namespace UI_Unilineal.Rendering.Avalonia.HitTesting;

public sealed record HitTestPolicy
{
    public HitTestPolicy(
        double toleranceDip = 6.0,
        double anchorRadiusDip = 8.0)
    {
        if (!double.IsFinite(toleranceDip) || toleranceDip <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(toleranceDip));
        }

        if (!double.IsFinite(anchorRadiusDip) || anchorRadiusDip <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(anchorRadiusDip));
        }

        ToleranceDip = toleranceDip;
        AnchorRadiusDip = anchorRadiusDip;
    }

    public double ToleranceDip { get; }

    public double AnchorRadiusDip { get; }
}
