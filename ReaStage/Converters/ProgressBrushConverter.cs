using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace ReaStage.Converters;

// progress 0..1 -> brush that is filled up to that fraction and transparent after it.
// The fill is painted as the border's own background, so the corner radius always
// clips it. A child element sized by width poked out past the rounded corners once it
// got narrower than the radius.
public class ProgressBrushConverter : IValueConverter
{
    private static readonly Color FillStart = Color.Parse("#1A4ADE80");
    private static readonly Color FillEnd = Color.Parse("#454ADE80");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double progress = value is double d ? Math.Clamp(d, 0, 1) : 0;

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(FillStart, 0),
                new GradientStop(FillEnd, progress),
                // Hard edge: the same offset twice turns the gradient off past it
                new GradientStop(Colors.Transparent, progress),
                new GradientStop(Colors.Transparent, 1)
            }
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
