using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Companion.Events;
using Companion.Services;
using Serilog;
using Companion.Logging;
using Avalonia.Threading;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing and displaying application logs
/// </summary>
public class LogViewerViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEventSubscriptionService _eventSubscriptionService;
    private int _duplicateCount;
    private DateTime _lastFlushTime = DateTime.Now;
    private string _lastMessage = string.Empty;
    private string _messageText;
    #endregion

    #region Public Properties
    /// <summary>
    /// Collection of log messages to display
    /// </summary>
    public ObservableCollection<string> LogMessages { get; set; }

    /// <summary>
    /// Gets or sets the current message text
    /// </summary>
    public string MessageText
    {
        get => _messageText;
        set => SetProperty(ref _messageText, value);
    }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of LogViewerViewModel
    /// </summary>
    public LogViewerViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _eventSubscriptionService = eventSubscriptionService ??
            throw new ArgumentNullException(nameof(eventSubscriptionService));
        
        SubscribeToEvents();
        
        InitializeCollections();

         // Subscribe to the LogQueue
         LogQueue.Subscribe(OnLogMessageQueued);
    }
    #endregion

    #region Initialization Methods
    private void InitializeCollections()
    {
        LogMessages = new ObservableCollection<string>();
    }

    private void SubscribeToEvents()
    {
        _eventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(AppMessageReceived);
        //_eventSubscriptionService.Subscribe<LogMessageEvent, string>(LogMessageReceived);   Remove this.
    }
    #endregion

    #region Event Handlers
    /// <summary>
    /// Handles incoming log messages and manages duplicate message handling
    /// </summary>
    private void OnLogMessageQueued(string message)
    {
        var formattedMessage = FormatLogMessage(message);

        if (message == _lastMessage)
        {
            HandleDuplicateMessage();
        }
        else
        {
            HandleNewMessage(message, formattedMessage);
        }
    }

    /// <summary>
    /// Handles incoming application messages
    /// </summary>
    private void AppMessageReceived(AppMessage message)
    {
        if (message.UpdateLogView)
        {
            var formattedMessage = FormatLogMessage(message.ToString());
            Dispatcher.UIThread.InvokeAsync(() => LogMessages.Insert(0, formattedMessage));
        }
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Formats a log message with timestamp
    /// </summary>
    private string FormatLogMessage(string message)
    {
        return $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
    }

    /// <summary>
    /// Handles processing of duplicate messages
    /// </summary>
    private void HandleDuplicateMessage()
    {
        _duplicateCount++;

        // Periodically flush duplicate count (every 5 seconds)
        if ((DateTime.Now - _lastFlushTime).TotalSeconds >= 5 && _duplicateCount > 0)
        {
            FlushDuplicateMessage();
        }
    }

    /// <summary>
    /// Handles processing of new messages
    /// </summary>
    private async void HandleNewMessage(string message, string formattedMessage)
    {
        // Flush any existing duplicate message summary
        if (_duplicateCount > 0)
        {
            await FlushDuplicateMessage();
        }

        // Reset duplicate counter and update last message
        _duplicateCount = 0;
        _lastMessage = message;

        // Add new message to log
        Dispatcher.UIThread.InvokeAsync(() => LogMessages.Insert(0, formattedMessage));
    }

    /// <summary>
    /// Flushes duplicate message summary to the log
    /// </summary>
    private async Task FlushDuplicateMessage()
    {
        if (_duplicateCount > 0)
        {
            var duplicateMessage = FormatLogMessage(
                $"[Last message repeated {_duplicateCount} times]");

            Dispatcher.UIThread.InvokeAsync(() => LogMessages.Insert(0, duplicateMessage));
            _duplicateCount = 0;
            _lastFlushTime = DateTime.Now;
        }
    }
    #endregion
}