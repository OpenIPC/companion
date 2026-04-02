using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class LogViewer : UserControl
{
    private const double BottomThreshold = 24;
    private LogViewerViewModel? _currentViewModel;

    public LogViewer()
    {
        InitializeComponent();

        //if (!Design.IsDesignMode) DataContext = new LogViewerViewModel();
        DataContext = App.ServiceProvider.GetService<LogViewerViewModel>();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => ScrollToLatest();
        DetachedFromVisualTree += (_, _) => UnsubscribeFromLogMessages(_currentViewModel);
        SizeChanged += OnSizeChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        UnsubscribeFromLogMessages(_currentViewModel);

        if (DataContext is LogViewerViewModel viewModel)
        {
            _currentViewModel = viewModel;
            viewModel.PropertyChanged += ViewModelOnPropertyChanged;
            viewModel.LogMessages.CollectionChanged += OnLogMessagesCollectionChanged;
            if (viewModel.FollowLatest)
                ScrollToLatest();
        }
        else
        {
            _currentViewModel = null;
        }
    }

    private void ViewModelOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not LogViewerViewModel viewModel)
            return;

        if (e.PropertyName == nameof(LogViewerViewModel.FollowLatest) && viewModel.FollowLatest)
            ScrollToLatest();
    }

    private void OnLogMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not LogViewerViewModel viewModel || e.Action != NotifyCollectionChangedAction.Add)
            return;

        if (viewModel.FollowLatest)
        {
            ScrollToLatest();
            return;
        }

        viewModel.NotifyMessageArrivedWhileDetached();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not LogViewerViewModel viewModel)
            return;

        if (IsNearBottom())
            viewModel.NotifyAttachedToLatest();
        else
            viewModel.NotifyDetachedFromLatest();
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_currentViewModel?.FollowLatest == true)
            ScrollToLatest();
    }

    private bool IsNearBottom()
    {
        var remaining = LogScrollViewer.Extent.Height - LogScrollViewer.Viewport.Height - LogScrollViewer.Offset.Y;
        return remaining <= BottomThreshold;
    }

    private void ScrollToLatest()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var targetY = LogScrollViewer.Extent.Height;
            LogScrollViewer.Offset = new Vector(LogScrollViewer.Offset.X, targetY);
        }, DispatcherPriority.Background);

        Dispatcher.UIThread.Post(() =>
        {
            var targetY = LogScrollViewer.Extent.Height;
            LogScrollViewer.Offset = new Vector(LogScrollViewer.Offset.X, targetY);
        }, DispatcherPriority.Loaded);
    }

    private void UnsubscribeFromLogMessages(LogViewerViewModel? viewModel)
    {
        if (viewModel is null)
            return;

        viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        viewModel.LogMessages.CollectionChanged -= OnLogMessagesCollectionChanged;
    }
}
