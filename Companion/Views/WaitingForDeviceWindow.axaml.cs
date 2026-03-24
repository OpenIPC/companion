using Avalonia.Controls;

namespace Companion.Views;

public partial class WaitingForDeviceWindow : Window
{
    public WaitingForDeviceWindow()
    {
        InitializeComponent();
    }

    private void OnCancelClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
