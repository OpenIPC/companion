using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class VRXTabView : UserControl
{
    public VRXTabView()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new VRXTabViewModel();
        DataContext = App.ServiceProvider.GetService<VRXTabViewModel>();
    }
}