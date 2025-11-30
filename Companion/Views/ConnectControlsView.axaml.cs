using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class ConnectControlsView : UserControl
{
    public ConnectControlsView()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new ConnectControlsViewModel();
        if (!Design.IsDesignMode) DataContext = App.ServiceProvider.GetService<ConnectControlsViewModel>();
    }
}