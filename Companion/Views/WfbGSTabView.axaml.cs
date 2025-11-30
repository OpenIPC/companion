using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class WfbGSTabView : UserControl
{
    public WfbGSTabView()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new WfbGSTabViewModel();
        DataContext = App.ServiceProvider.GetService<WfbGSTabViewModel>();
    }
}