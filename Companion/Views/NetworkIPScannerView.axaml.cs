using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class NetworkIPScannerView : UserControl
{
    public NetworkIPScannerView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode)
        {
            DataContext = App.ServiceProvider.GetService<SetupTabViewModel>();
        }
    }
}