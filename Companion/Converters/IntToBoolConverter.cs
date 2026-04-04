using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out var paramInt))
            return intValue == paramInt;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string paramStr && int.TryParse(paramStr, out var paramInt))
            return paramInt;
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
