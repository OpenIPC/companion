using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Companion.Events;
using Companion.Services;
using Companion.ViewModels;
using Serilog;

namespace Companion.Views;

public partial class MainView : UserControl
{
    private const double MaxLogPanelRatio = 0.4;
    private const double MinExpandedHeight = 80; // header bar + 3 log lines
    private const double CollapsedHeight = 20;
    private MainViewModel? _viewModel;

    private RowDefinition LogRow
    {
        get
        {
            var mainGrid = this.FindControl<Grid>("MainGrid");
            return mainGrid!.RowDefinitions[3];
        }
    }

    public MainView()
    {
        var stopwatch = Stopwatch.StartNew();
        InitializeComponent();
        Log.ForContext<MainView>().Information("Startup timing: MainView.InitializeComponent completed in {ElapsedMs} ms.",
            stopwatch.Elapsed.TotalMilliseconds);

        if (!Design.IsDesignMode)
        {
            var resolveViewModelStopwatch = Stopwatch.StartNew();
            _viewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
            Log.ForContext<MainView>().Information("Startup timing: MainViewModel resolved in {ElapsedMs} ms.",
                resolveViewModelStopwatch.Elapsed.TotalMilliseconds);
            DataContext = _viewModel;

            var subscriptionsStopwatch = Stopwatch.StartNew();
            var eventSubscriptionService = App.ServiceProvider.GetRequiredService<IEventSubscriptionService>();
            eventSubscriptionService.Subscribe<TabSelectionChangeEvent, string>(OnTabSelectionChanged);

            // Set initial log panel height from preferences
            ApplyLogPanelHeight();
            UpdateChevron();

            // Track collapse/expand changes
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            LogSplitter.DragDelta += OnSplitterDragDelta;
            LogSplitter.DragCompleted += OnSplitterDragCompleted;
            Log.ForContext<MainView>().Information("Startup timing: MainView post-initialize wiring completed in {ElapsedMs} ms.",
                subscriptionsStopwatch.Elapsed.TotalMilliseconds);
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

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsLogPanelCollapsed))
        {
            ApplyLogPanelHeight();
            UpdateChevron();
        }
    }

    private void ApplyLogPanelHeight()
    {
        if (_viewModel == null) return;

        var logRow = LogRow;
        if (_viewModel.IsLogPanelCollapsed)
        {
            logRow.MinHeight = CollapsedHeight;
            logRow.Height = new GridLength(CollapsedHeight, GridUnitType.Pixel);
        }
        else
        {
            logRow.MinHeight = MinExpandedHeight;
            var height = Math.Max(MinExpandedHeight, _viewModel.LogPanelHeight);
            logRow.Height = new GridLength(height, GridUnitType.Pixel);
        }
    }

    private void UpdateChevron()
    {
        if (_viewModel != null)
            LogChevron.Text = _viewModel.IsLogPanelCollapsed ? "▸" : "▾";
    }

    private void OnSplitterDragDelta(object? sender, VectorEventArgs e)
    {
        // e.Vector.Y < 0 means dragging up = making log panel bigger
        if (e.Vector.Y >= 0) return;

        var mainGrid = LogPanel.Parent as Grid;
        if (mainGrid == null) return;

        var logRow = LogRow;
        var maxHeight = mainGrid.Bounds.Height * MaxLogPanelRatio;
        if (logRow.ActualHeight > maxHeight)
            logRow.Height = new GridLength(maxHeight, GridUnitType.Pixel);
    }

    private void OnSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        if (_viewModel == null || _viewModel.IsLogPanelCollapsed) return;

        var mainGrid = LogPanel.Parent as Grid;
        if (mainGrid == null) return;

        var logRow = LogRow;
        var maxHeight = mainGrid.Bounds.Height * MaxLogPanelRatio;
        var height = Math.Min(logRow.ActualHeight, maxHeight);
        height = Math.Max(height, MinExpandedHeight);

        _viewModel.LogPanelHeight = height;
    }

    private void OnDrawerLinePressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.ToggleTabsCommand.Execute(null);
    }
}
