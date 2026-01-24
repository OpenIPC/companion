using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Companion.Converters;

public class TagsToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ObservableCollection<string> tags)
        {
            return string.Join(", ", tags);
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
