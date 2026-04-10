using System;
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
    private bool _isProgrammaticScroll;
    private bool _scrollToLatestQueued;

    public LogViewer()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => ScrollToLatest();
        DetachedFromVisualTree += (_, _) => UnsubscribeFromLogMessages(_currentViewModel);
        SizeChanged += OnSizeChanged;

        //if (!Design.IsDesignMode) DataContext = new LogViewerViewModel();
        DataContext = App.ServiceProvider.GetService<LogViewerViewModel>();
        OnDataContextChanged(this, EventArgs.Empty);
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

        if (_isProgrammaticScroll)
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
        if (_scrollToLatestQueued)
            return;

        _scrollToLatestQueued = true;

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                SetScrollOffsetToLatest();
            }
            finally
            {
                _scrollToLatestQueued = false;
            }
        }, DispatcherPriority.Background);
    }

    private void SetScrollOffsetToLatest()
    {
        _isProgrammaticScroll = true;

        try
        {
            var targetY = Math.Max(0, LogScrollViewer.Extent.Height - LogScrollViewer.Viewport.Height);
            LogScrollViewer.Offset = new Vector(LogScrollViewer.Offset.X, targetY);
        }
        finally
        {
            Dispatcher.UIThread.Post(() => _isProgrammaticScroll = false, DispatcherPriority.Background);
        }
    }

    private void UnsubscribeFromLogMessages(LogViewerViewModel? viewModel)
    {
        if (viewModel is null)
            return;

        viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        viewModel.LogMessages.CollectionChanged -= OnLogMessagesCollectionChanged;
    }
}
