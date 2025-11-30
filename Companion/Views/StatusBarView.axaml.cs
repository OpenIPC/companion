using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class StatusBarView : UserControl
{
    public StatusBarView()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new StatusBarViewModel();
        DataContext = App.ServiceProvider.GetService<StatusBarViewModel>();
    }
}