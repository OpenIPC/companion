using System.ComponentModel;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class CameraSettingsTabView : UserControl, INotifyPropertyChanged
{
    public CameraSettingsTabView()
    {
        InitializeComponent();

        if (!Design.IsDesignMode) DataContext = App.ServiceProvider.GetService<CameraSettingsTabViewModel>();
    }
}