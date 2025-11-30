using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class SetupRadxaButtonsView : UserControl
{
    public SetupRadxaButtonsView()
    {
        InitializeComponent();
        if (!Design.IsDesignMode) DataContext = App.ServiceProvider.GetService<SetupTabViewModel>();
    }
}