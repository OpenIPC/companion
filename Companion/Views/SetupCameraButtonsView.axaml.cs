using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class SetupCameraButtonsView : UserControl
{
    public SetupCameraButtonsView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode) DataContext = App.ServiceProvider.GetService<SetupTabViewModel>();

        //ScriptFilesActionButton.IsEnabled = false;
        //CameraKeyActionButton.IsEnabled = false;
    }


    // private void ButtonsComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    // {
    //     if (sender is ComboBox comboBox)
    //     {
    //         // Get the selected index
    //         var selectedIndex = comboBox.SelectedIndex;
    //
    //         // Example: Enable/disable a button based on the selected index
    //         switch (comboBox.Name)
    //         {
    //             case "ScriptFilesActionComboBox":
    //                 ScriptFilesActionButton.IsEnabled = selectedIndex > -1;
    //                 break;
    //
    //             case "CameraKeyActionButton":
    //                 CameraKeyActionButton.IsEnabled = selectedIndex > -1;
    //                 break;
    //
    //             case "SensorComboBox":
    //                 SensorComboBox.IsEnabled = selectedIndex > -1;
    //                 break;
    //
    //             default:
    //                 Log.Debug($"Unhandled ComboBox: {comboBox.Name}");
    //                 break;
    //         }
    //     }
    // }
}