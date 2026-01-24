using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class BooleanToWidthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter == null || value is not bool flag)
            return 0d;

        var widths = parameter.ToString()?.Split(',') ?? Array.Empty<string>();
        if (widths.Length < 2)
            return 0d;

        if (!double.TryParse(widths[0], out var trueWidth) ||
            !double.TryParse(widths[1], out var falseWidth))
            return 0d;

        return flag ? trueWidth : falseWidth;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
