using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class SetupTabView : UserControl
{
    public SetupTabView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode) DataContext = App.ServiceProvider.GetService<SetupTabViewModel>();
    }
}