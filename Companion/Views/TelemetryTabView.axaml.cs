using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class TelemetryTabView : UserControl
{
    public TelemetryTabView()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new TelemetryTabViewModel();
        DataContext = App.ServiceProvider.GetService<TelemetryTabViewModel>();
    }
}