using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Domain.Tests.Scene;

public sealed class GeometryTests
{
    [Fact]
    public void MmRect_RejectsNonPositiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MmRect(0, 0, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MmRect(0, 0, 10, -1));
    }

    [Theory]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    [InlineData(0, double.NegativeInfinity)]
    public void MmPoint_RejectsNonFiniteCoordinates(double x, double y)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new MmPoint(x, y));
    }

    [Fact]
    public void MmRect_ComputesRightAndBottom()
    {
        var rect = new MmRect(2.5, 3.5, 10, 20);

        Assert.Equal(12.5, rect.Right);
        Assert.Equal(23.5, rect.Bottom);
    }
}
