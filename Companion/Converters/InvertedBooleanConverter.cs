using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class InvertedBooleanConverter : IValueConverter

{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue) return !booleanValue;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool booleanValue) return !booleanValue;
        return false;
    }
}