using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ReaStage.Converters;

// value = scale factor (e.g. 1.25), parameter = base font size -> scaled size
public class FontScaleConverter : IValueConverter
{
    // Used when the parameter is missing or malformed; a zero font size would
    // render the text invisible
    private const double FallbackFontSize = 16;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double scale = value is double d && d > 0 ? d : 1.0;
        double baseSize = parameter is string s
            && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && parsed > 0
            ? parsed
            : FallbackFontSize;

        return baseSize * scale;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
