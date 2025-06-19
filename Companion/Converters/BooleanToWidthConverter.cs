using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class BooleanToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var widths = parameter.ToString().Split(',');
        return (bool)value ? double.Parse(widths[0]) : double.Parse(widths[1]);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}