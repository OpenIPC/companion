using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.LogicalTree;
using Companion.ViewModels;

namespace Companion.Converters;

public class CanConnectConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Try to get the DataContext from the current view model
        if (Avalonia.Application.Current?.DataContext is PresetsTabViewModel viewModel)
        {
            return viewModel.CanConnect;
        }

        // Alternative approach: try to find the view model through the logical tree
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;
            if (mainWindow != null)
            {
                // Recursively search through logical children
                var mainViewModel = FindDataContext<PresetsTabViewModel>(mainWindow);
                if (mainViewModel != null)
                {
                    return mainViewModel.CanConnect;
                }
            }
        }

        // Fallback to default
        return false;
    }

    private T? FindDataContext<T>(ILogical logical) where T : class
    {
        // Check the current logical's DataContext
        if (logical is Control control && control.DataContext is T matchingViewModel)
        {
            return matchingViewModel;
        }

        // Recursively search through logical children
        foreach (var child in logical.LogicalChildren)
        {
            var result = FindDataContext<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}