using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class BooleanToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var texts = parameter.ToString().Split(',');
        return (bool)value ? texts[1] : texts[0];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}