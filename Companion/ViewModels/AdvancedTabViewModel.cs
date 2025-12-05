using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

public partial class AdvancedTabViewModel : ViewModelBase
{
    #region Fields

    private readonly IYamlConfigService _yamlConfigService;

    #endregion

    #region Properties

    [ObservableProperty] private bool _canConnect;

    public bool IsAlinkDroneDisabled => !IsAlinkDroneEnabled;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsAlinkDroneDisabled))]
    private bool _isAlinkDroneEnabled;

    public bool InstalledButNotRunning => IsAdaptiveLinkInstalled && !IsAdaptiveLinkRunning;

    public bool NotInstalled => !IsAdaptiveLinkInstalled;

    [ObservableProperty] private string _adaptiveLinkInstallStatus = "";


    [ObservableProperty] private bool _isAdaptiveLinkInstalled;

    [ObservableProperty] private bool _isAdaptiveLinkRunning;

    [ObservableProperty] private string _adaptiveLinkVersion = "Unknown";

    #endregion

    #region Commands

    public IAsyncRelayCommand CheckAdaptiveLinkStatusCommand { get; }
    public IAsyncRelayCommand GenerateSystemReportCommand { get; }
    public IAsyncRelayCommand ViewSystemLogsCommand { get; }
    public IAsyncRelayCommand NetworkDiagnosticsCommand { get; }

    // Command property for toggling alink_drone
    public IAsyncRelayCommand ToggleAlinkDroneCommand { get; set; }

    #endregion

    public AdvancedTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IYamlConfigService yamlConfigService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        SubscribeToEvents();

        _yamlConfigService = yamlConfigService ?? throw new ArgumentNullException(nameof(yamlConfigService));

        // Initialize commands
        CheckAdaptiveLinkStatusCommand = new AsyncRelayCommand(CheckAdaptiveLinkStatusAsync);
        GenerateSystemReportCommand = new AsyncRelayCommand(GenerateSystemReportAsync);
        ViewSystemLogsCommand = new AsyncRelayCommand(ViewSystemLogsAsync);
        NetworkDiagnosticsCommand = new AsyncRelayCommand(NetworkDiagnosticsAsync);
        ToggleAlinkDroneCommand = new AsyncRelayCommand(async () => await ToggleAlinkDroneAsync());
    }

    #region Adaptive Link Methods

    partial void OnIsAlinkDroneEnabledChanged(bool value)
    {
        // This will be called when the toggle switch changes
        _ = Task.Run(async () => await ToggleAlinkDroneAsync());
    }
    
    // Method to toggle the alink_drone status
    private async Task ToggleAlinkDroneAsync()
    {
        if (_isUpdatingAlinkDroneStatus)
            return;

        _isUpdatingAlinkDroneStatus = true;
        try
        {
            if (!DeviceConfig.Instance.CanConnect)
            {
                UpdateUIMessage("Device not connected. Cannot toggle Adaptive Link status.");
                return;
            }

            // Toggle the value - this will be from the current to the new state
            bool newValue = IsAlinkDroneEnabled;

            // Execute the appropriate command based on the NEW value
            string command = newValue
                ? DeviceCommands.AddAlinkDroneToRcLocal
                : DeviceCommands.RemoveAlinkDroneFromRcLocal;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 seconds timeout
            var result =
                await SshClientService.ExecuteCommandWithResponseAsync(DeviceConfig.Instance, command, cts.Token);

            // Update the property WITHOUT triggering the changed handler (use SetProperty directly)
            IsAlinkDroneEnabled = newValue;

            UpdateUIMessage(IsAlinkDroneEnabled
                ? "Adaptive Link enabled on boot."
                : "Adaptive Link disabled on boot.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error toggling Adaptive Link status");
            UpdateUIMessage($"Error toggling Adaptive Link status: {ex.Message}");
        }
        finally
        {
            _isUpdatingAlinkDroneStatus = false;
        }
    }

    private bool _isUpdatingAlinkDroneStatus = false;
    
    private async Task CheckAlinkDroneStatusAsync()
    {
        Logger.Verbose("Checking Adaptive Link status");
        if (!DeviceConfig.Instance.CanConnect || _isUpdatingAlinkDroneStatus)
            return;

        _isUpdatingAlinkDroneStatus = true;
        try
        {
            var cts = new CancellationTokenSource(10000); // 10 seconds timeout
            var cmdResult = await SshClientService.ExecuteCommandWithResponseAsync(
                DeviceConfig.Instance,
                DeviceCommands.IsAlinkDroneEnabled,
                cts.Token);

            bool newStatus = cmdResult?.Result?.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            // Only update if status actually changed to avoid recursive property change notifications
            if (IsAlinkDroneEnabled != newStatus)
            {
                IsAlinkDroneEnabled = newStatus;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error checking Adaptive Link status");
        }
        finally
        {
            _isUpdatingAlinkDroneStatus = false;
        }
    }


    private async Task CheckAdaptiveLinkStatusAsync()
    {
        try
        {
            var deviceConfig = DeviceConfig.Instance;
            if (!deviceConfig.CanConnect)
            {
                UpdateUIMessage("Device not connected. Cannot check Adaptive Link status.");
                return;
            }

            UpdateUIMessage("Checking Adaptive Link status...");

            // Reset status properties
            IsAdaptiveLinkInstalled = false;
            IsAdaptiveLinkRunning = false;
            AdaptiveLinkVersion = "Unknown";

            // Check if the alink_drone executable exists
            string checkCommand = "[ -f /usr/bin/alink_drone ] && echo 'INSTALLED' || echo 'NOT_INSTALLED'";
            var cts = new CancellationTokenSource(10000); // 10 seconds
            var cancellationToken = cts.Token;

            var checkResult =
                await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig, checkCommand, cancellationToken);
            
            string statusOutput = checkResult?.Result?.Trim() ?? "Failed to check status";

            if (statusOutput != "INSTALLED")
            {
                UpdateUIMessage("Adaptive Link is not installed on this device.");
                return;
            }

            // File exists, so Adaptive Link is installed
            IsAdaptiveLinkInstalled = true;

            // Check if it's running
            cts = new CancellationTokenSource(10000);
            cancellationToken = cts.Token;
            var runningCheck = await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig,
                "ps | grep alink_drone | grep -v grep | wc -l", cancellationToken);

            // If count > 0, it's running
            int processCount = int.TryParse(runningCheck?.Result?.Trim(), out var count) ? count : 0;
            IsAdaptiveLinkRunning = processCount > 0;

            // Check configuration file
            cts = new CancellationTokenSource(10000);
            cancellationToken = cts.Token;
            var configResult = await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig,
                "cat /etc/alink.conf 2>/dev/null || echo 'Configuration not found'", cancellationToken);

            string configOutput = configResult?.Result ?? "No configuration available";

            StringBuilder statusInfo = new StringBuilder();
            statusInfo.AppendLine(
                $"Adaptive Link is installed and {(IsAdaptiveLinkRunning ? "running" : "not running")}.");


            statusInfo.AppendLine();
            statusInfo.AppendLine("Configuration:");
            statusInfo.AppendLine(configOutput);

            UpdateUIMessage(statusInfo.ToString());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to check Adaptive Link status");
            UpdateUIMessage($"Error checking Adaptive Link status: {ex.Message}");
        }
    }

    #endregion

    #region Initialization Methods

    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
        // Subscribe to alink drone status updates
        EventSubscriptionService.Subscribe<AlinkDroneStatusEvent, bool>(status => IsAlinkDroneEnabled = status);
    }

    #endregion

    #region Debug Tool Methods

    private async Task GenerateSystemReportAsync()
    {
        try
        {
            var deviceConfig = DeviceConfig.Instance;
            if (!deviceConfig.CanConnect)
            {
                UpdateUIMessage("Device not connected. Cannot generate system report.");
                return;
            }

            UpdateUIMessage("Generating system report...");

            // System commands to gather information
            var systemCommands = new List<(string Section, string Command)>
            {
                ("System Information", "uname -a"),
                ("CPU Info", "cat /proc/cpuinfo | grep -i 'model name\\|processor'"),
                ("Memory Info", "free -h"),
                ("Disk Usage", "df -h"),
                ("Network Interfaces", "ip addr"),
                ("Loaded Modules", "lsmod"),
                ("Active Services", "systemctl list-units --type=service --state=running")
            };

            // Configuration files to include
            var configFiles = new List<string>
            {
                "/etc/os-release",
                "/etc/majestic.yaml",
                "/etc/wfb.yaml",
                "/etc/wfb.conf",
                "/etc/telemetry.conf",
                "/etc/alink.conf",
                "/etc/vtxmenu.ini",
                "/etc/txprofiles.conf",
                "/etc/datalink.conf"
                // Add new files here as needed
            };

            // Build the command string
            var commandBuilder = new StringBuilder();

            // Add system commands
            foreach (var (section, command) in systemCommands)
            {
                commandBuilder.Append($"echo '=== {section} ===' && {command} && ");
            }

            // Add configuration files section header
            commandBuilder.Append("echo '=== OpenIPC Configurations ===' && ");

            // Add each configuration file
            foreach (var file in configFiles)
            {
                string filename = Path.GetFileName(file);
                commandBuilder.Append($"echo '--- {file} ---' && ");
                commandBuilder.Append($"cat {file} 2>/dev/null || echo 'File not found' && ");
            }

            // Remove the trailing " && "
            string reportCommands = commandBuilder.ToString();
            if (reportCommands.EndsWith(" && "))
            {
                reportCommands = reportCommands.Substring(0, reportCommands.Length - 4);
            }

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 seconds timeout
            var result =
                await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig, reportCommands, cts.Token);

            if (result == null || string.IsNullOrEmpty(result.Result))
            {
                UpdateUIMessage("Failed to generate system report. No data received.");
                return;
            }

            string reportContent = result.Result;

            // Let the user know the report was generated
            UpdateUIMessage("System report generated. Select a location to save it...");

            // Now prompt the user for a save location
            // Use Avalonia's SaveFileDialog to let the user pick a location
            var saveFileDialog = new Avalonia.Controls.SaveFileDialog
            {
                Title = "Save System Report",
                DefaultExtension = "txt",
                InitialFileName = $"system_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Filters = new List<Avalonia.Controls.FileDialogFilter>
                {
                    new Avalonia.Controls.FileDialogFilter
                    {
                        Name = "Text Files",
                        Extensions = new List<string> { "txt" }
                    },
                    new Avalonia.Controls.FileDialogFilter
                    {
                        Name = "All Files",
                        Extensions = new List<string> { "*" }
                    }
                }
            };

            // Get the main window to show the dialog
            var mainWindow =
                Avalonia.Application.Current.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

            if (mainWindow == null)
            {
                UpdateUIMessage("Could not show save dialog. Using default location.");
                // Save to a default location if we can't show the dialog
                string defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"system_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                File.WriteAllText(defaultPath, reportContent);
                UpdateUIMessage($"System report saved to: {defaultPath}");
                return;
            }

            // Show the save dialog and get the selected path
            string filePath = await saveFileDialog.ShowAsync(mainWindow);

            if (string.IsNullOrEmpty(filePath))
            {
                // User canceled the save operation
                UpdateUIMessage("Save operation canceled.");
                return;
            }

            // Save the report to the selected file
            File.WriteAllText(filePath, reportContent);

            UpdateUIMessage($"System report saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to generate system report");
            UpdateUIMessage($"Error generating system report: {ex.Message}");
        }
    }

    private async Task ViewSystemLogsAsync()
    {
        try
        {
            var deviceConfig = DeviceConfig.Instance;
            if (!deviceConfig.CanConnect)
            {
                UpdateUIMessage("Device not connected. Cannot view system logs.");
                return;
            }

            UpdateUIMessage("Retrieving system logs...");

            // Execute command to get both journalctl and readlog output
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 seconds timeout

            // Build combined log command
            string logCommand =
                "echo '=== Journal Logs (Last 100 Entries) ===' && " +
                "journalctl -n 100 2>/dev/null || echo 'journalctl not available' && " +
                "echo -e '\\n\\n=== OpenIPC readlog Output ===' && " +
                "logread 2>/dev/null || echo 'logread command not available'";

            var logResult = await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig, logCommand, cts.Token);

            if (logResult == null || string.IsNullOrEmpty(logResult.Result))
            {
                UpdateUIMessage("Failed to retrieve system logs. No data received.");
                return;
            }

            string logContent = logResult.Result;

            // Let the user know the logs were retrieved
            UpdateUIMessage("System logs retrieved. Select a location to save them...");

            // Prompt the user for a save location using SaveFileDialog
            var saveFileDialog = new Avalonia.Controls.SaveFileDialog
            {
                Title = "Save System Logs",
                DefaultExtension = "txt",
                InitialFileName = $"system_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Filters = new List<Avalonia.Controls.FileDialogFilter>
                {
                    new Avalonia.Controls.FileDialogFilter
                    {
                        Name = "Text Files",
                        Extensions = new List<string> { "txt" }
                    },
                    new Avalonia.Controls.FileDialogFilter
                    {
                        Name = "All Files",
                        Extensions = new List<string> { "*" }
                    }
                }
            };

            // Get the main window to show the dialog
            var mainWindow =
                Avalonia.Application.Current.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

            if (mainWindow == null)
            {
                UpdateUIMessage("Could not show save dialog. Using default location.");
                // Save to a default location if we can't show the dialog
                string defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"system_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                File.WriteAllText(defaultPath, logContent);
                UpdateUIMessage($"System logs saved to: {defaultPath}");
                return;
            }

            // Show the save dialog and get the selected path
            string filePath = await saveFileDialog.ShowAsync(mainWindow);

            if (string.IsNullOrEmpty(filePath))
            {
                // User canceled the save operation
                UpdateUIMessage("Save operation canceled.");
                return;
            }

            // Save the logs to the selected file
            File.WriteAllText(filePath, logContent);

            UpdateUIMessage($"System logs saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve system logs");
            UpdateUIMessage($"Error retrieving system logs: {ex.Message}");
        }
    }

    private async Task NetworkDiagnosticsAsync()
    {
        try
        {
            var deviceConfig = DeviceConfig.Instance;
            if (!deviceConfig.CanConnect)
            {
                UpdateUIMessage("Device not connected. Cannot run network diagnostics.");
                return;
            }

            UpdateUIMessage("Running network diagnostics...");

            // Execute commands to diagnose network issues
            string diagnosticCommands =
                "echo '=== Network Interfaces ===' && " +
                "ip addr && " +
                "echo '=== Routing Table ===' && " +
                "ip route && " +
                "echo '=== DNS Configuration ===' && " +
                "cat /etc/resolv.conf && " +
                "echo '=== Ping Test (Google DNS) ===' && " +
                "ping -c 4 8.8.8.8 || echo 'Ping failed' && " +
                "echo '=== Wireless Interfaces ===' && " +
                "iwconfig 2>/dev/null || echo 'iwconfig not available' && " +
                "echo '=== Wireless Status ===' && " +
                "iwconfig wlan0 2>/dev/null || echo 'iwconfig wlan0 not available' && " +
                "echo '=== Network Connection Stats ===' && " +
                "netstat -tuln || echo 'netstat not available'";

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)); // 60 seconds timeout
            var diagnosticsResult =
                await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig, diagnosticCommands, cts.Token);

            if (diagnosticsResult == null || string.IsNullOrEmpty(diagnosticsResult.Result))
            {
                UpdateUIMessage("Failed to run network diagnostics. No data received.");
                return;
            }

            string diagnosticsContent = diagnosticsResult.Result;

            // Let the user know the diagnostics were completed
            UpdateUIMessage("Network diagnostics completed. Select a location to save the results...");

            // Prompt the user for a save location using SaveFileDialog
            var saveFileDialog = new Avalonia.Controls.SaveFileDialog
            {
                Title = "Save Network Diagnostics",
                DefaultExtension = "txt",
                InitialFileName = $"network_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                Filters = new List<Avalonia.Controls.FileDialogFilter>
                {
                    new Avalonia.Controls.FileDialogFilter
                    {
                        Name = "Text Files",
                        Extensions = new List<string> { "txt" }
                    },
                    new Avalonia.Controls.FileDialogFilter
                    {
                        Name = "All Files",
                        Extensions = new List<string> { "*" }
                    }
                }
            };

            // Get the main window to show the dialog
            var mainWindow =
                Avalonia.Application.Current.ApplicationLifetime is
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

            if (mainWindow == null)
            {
                UpdateUIMessage("Could not show save dialog. Using default location.");
                // Save to a default location if we can't show the dialog
                string defaultPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    $"network_diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                File.WriteAllText(defaultPath, diagnosticsContent);
                UpdateUIMessage($"Network diagnostics saved to: {defaultPath}");
                return;
            }

            // Show the save dialog and get the selected path
            string filePath = await saveFileDialog.ShowAsync(mainWindow);

            if (string.IsNullOrEmpty(filePath))
            {
                // User canceled the save operation
                UpdateUIMessage("Save operation canceled.");
                return;
            }

            // Save the diagnostics to the selected file
            File.WriteAllText(filePath, diagnosticsContent);

            UpdateUIMessage($"Network diagnostics saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to run network diagnostics");
            UpdateUIMessage($"Error running network diagnostics: {ex.Message}");
        }
    }

    #endregion

    #region Event Handlers

    private void OnAppMessage(AppMessage message)
    {
        // Use InvokeAsync instead of Invoke for async lambdas
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            bool wasConnected = CanConnect;
            if (CanConnect != message.CanConnect)
            {
                CanConnect = message.CanConnect;
            }

            OnPropertyChanged(nameof(CanConnect));

            if (!wasConnected && CanConnect)
            {
                try 
                {
                    Logger.Debug("Starting Adaptive Link status check");
                    await CheckAdaptiveLinkStatusAsync();
                    Logger.Debug("Starting Alink Drone status check");
                    await CheckAlinkDroneStatusAsync();
                    Logger.Debug("Completed initialization checks");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error during initialization checks");
                }
            }
        });
    }

    #endregion
}