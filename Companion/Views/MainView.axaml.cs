using System.Linq;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Companion.Events;
using Companion.Services;
using Companion.ViewModels;

namespace Companion.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        if (!Design.IsDesignMode)
        {
            // Resolve MainViewModel from the DI container
            DataContext = App.ServiceProvider.GetRequiredService<MainViewModel>();

            // Subscribe to TabSelectionChangeEvent
            var _eventSubscriptionService = App.ServiceProvider.GetRequiredService<IEventSubscriptionService>();

            _eventSubscriptionService.Subscribe<TabSelectionChangeEvent, string>(OnTabSelectionChanged);
        }
    }

    private void OnTabSelectionChanged(string selectedTab)
    {
        var tabControl = this.FindControl<TabControl>("MainTabControl");
        if (tabControl?.Items == null) return;

        var targetTab = tabControl.Items
            .OfType<TabItemViewModel>()
            .FirstOrDefault(tab => tab.TabName == selectedTab);

        if (targetTab != null) tabControl.SelectedItem = targetTab;
    }
}