using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class LogViewer : UserControl
{
    public LogViewer()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new LogViewerViewModel();
        DataContext = App.ServiceProvider.GetService<LogViewerViewModel>();
    }
}