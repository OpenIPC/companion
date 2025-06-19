using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class AdvancedTabView : UserControl
{
    public AdvancedTabView()
    {
        if (!Design.IsDesignMode) 
            DataContext = App.ServiceProvider.GetService<AdvancedTabViewModel>();
        
        InitializeComponent();
    }
    
    
}