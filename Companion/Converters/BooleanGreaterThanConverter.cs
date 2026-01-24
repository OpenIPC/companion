using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

/// <summary>
/// Converts a numeric value to a boolean by comparing it with a threshold
/// </summary>
public class BooleanGreaterThanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
            
        double threshold;
        if (!double.TryParse(parameter.ToString(), out threshold))
            return false;
            
        if (value is double doubleValue)
            return doubleValue > threshold;
            
        if (value is int intValue)
            return intValue > threshold;
            
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
