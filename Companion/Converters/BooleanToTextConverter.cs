using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class BooleanToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter == null)
            return string.Empty;

        var texts = parameter.ToString()?.Split(',') ?? Array.Empty<string>();
        if (texts.Length < 2 || value is not bool flag)
            return string.Empty;

        return flag ? texts[1] : texts[0];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
