using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ReaStage.Converters;

// [container width, progress 0..1] -> pixel width of the progress fill
public class ProgressWidthConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 2 && values[0] is double width && values[1] is double progress)
        {
            return Math.Max(0, width * Math.Clamp(progress, 0, 1));
        }

        return 0.0;
    }
}
