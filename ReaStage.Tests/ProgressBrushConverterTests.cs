using Avalonia.Media;
using ReaStage.Converters;
using System.Globalization;

namespace ReaStage.Tests;

public class ProgressBrushConverterTests
{
    private static LinearGradientBrush Convert(object? progress)
    {
        object result = new ProgressBrushConverter().Convert(progress, typeof(IBrush), null, CultureInfo.InvariantCulture);
        return Assert.IsType<LinearGradientBrush>(result);
    }

    [Fact]
    public void HalfWay_HasHardEdgeAtProgress()
    {
        LinearGradientBrush brush = Convert(0.5);

        Assert.Equal([0, 0.5, 0.5, 1], brush.GradientStops.Select(s => s.Offset));
        Assert.NotEqual(Colors.Transparent, brush.GradientStops[1].Color);
        Assert.Equal(Colors.Transparent, brush.GradientStops[2].Color);
    }

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(1.5, 1)]
    public void OutOfRange_IsClamped(double progress, double expected)
    {
        LinearGradientBrush brush = Convert(progress);

        Assert.Equal(expected, brush.GradientStops[1].Offset);
    }

    [Fact]
    public void NotADouble_IsTreatedAsEmpty()
    {
        LinearGradientBrush brush = Convert(null);

        Assert.Equal(0, brush.GradientStops[1].Offset);
    }
}
