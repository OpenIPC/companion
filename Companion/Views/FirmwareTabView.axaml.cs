using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class FirmwareTabView : UserControl
{
    public FirmwareTabView()
    {
        InitializeComponent();

        if (!Design.IsDesignMode) 
            DataContext = App.ServiceProvider.GetService<FirmwareTabViewModel>();
    }
}