using System;
using System.Globalization;
using Avalonia.Data.Converters;
using ExCSS;

namespace Companion.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isVisible)
            return isVisible ? Visibility.Visible : Visibility.Collapse;

        return Visibility.Collapse;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
