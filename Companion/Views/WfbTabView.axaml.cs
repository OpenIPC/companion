using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class WfbTabView : UserControl
{
    public WfbTabView()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
            // Resolve the DataContext from the DI container at runtime
            DataContext = App.ServiceProvider.GetService<WfbTabViewModel>();
    }
}