using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Companion.Converters;

/// <summary>
/// Converts a numeric value to a color based on a threshold
/// </summary>
public class PowerThresholdColorConverter : IValueConverter
{
    /// <summary>
    /// Threshold value at which color changes
    /// </summary>
    public double Threshold { get; set; } = 25;

    /// <summary>
    /// Color when value is below threshold
    /// </summary>
    public ISolidColorBrush NormalColor { get; set; } = new SolidColorBrush(Colors.Black);

    /// <summary>
    /// Color when value exceeds threshold
    /// </summary>
    public ISolidColorBrush WarningColor { get; set; } = new SolidColorBrush(Colors.Red);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue > Threshold ? WarningColor : NormalColor;
        }
            
        if (value is int intValue)
        {
            return intValue > Threshold ? WarningColor : NormalColor;
        }
            
        return NormalColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
