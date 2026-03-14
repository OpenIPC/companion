using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

public partial class PreferencesTabViewModel : ViewModelBase
{
    private readonly IPreferencesService _preferencesService;
    private UserPreferences _preferences;
    private bool _isLoading;

    [ObservableProperty] private bool _canConnect;
    [ObservableProperty] private bool _checkForUpdatesOnStartup;
    [ObservableProperty] private bool _firmwareFocusedMode;
    [ObservableProperty] private string _preferredFirmwareSource = string.Empty;
    [ObservableProperty] private string _statusMessage = "Preferences are saved automatically.";

    public ObservableCollection<string> FirmwareSources { get; } = new()
    {
        "OpenIPC Builder",
        "Greg APFPV"
    };

    public IAsyncRelayCommand GenerateSystemReportCommand { get; }
    public IRelayCommand OpenLogsFolderCommand { get; }

    public PreferencesTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IPreferencesService preferencesService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _preferencesService = preferencesService;
        _preferences = _preferencesService.Load();
        _isLoading = true;
        CheckForUpdatesOnStartup = _preferences.CheckForUpdatesOnStartup;
        FirmwareFocusedMode = _preferences.FirmwareFocusedMode;
        PreferredFirmwareSource = _preferences.PreferredFirmwareSource;
        EnsureValidFirmwareSource();
        _isLoading = false;

        GenerateSystemReportCommand = new AsyncRelayCommand(GenerateSystemReportAsync);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        SubscribeToEvents();
    }

    partial void OnCheckForUpdatesOnStartupChanged(bool value)
    {
        SavePreferences();
    }

    partial void OnPreferredFirmwareSourceChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        SavePreferences();
    }

    partial void OnFirmwareFocusedModeChanged(bool value)
    {
        SavePreferences();
    }

    private void EnsureValidFirmwareSource()
    {
        if (FirmwareSources.Contains(PreferredFirmwareSource))
            return;

        PreferredFirmwareSource = FirmwareSources[0];
    }

    private void SavePreferences()
    {
        if (_isLoading)
            return;

        _preferences.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
        _preferences.FirmwareFocusedMode = FirmwareFocusedMode;
        _preferences.PreferredFirmwareSource = PreferredFirmwareSource;
        _preferencesService.Save(_preferences);
        StatusMessage = $"Saved {DateTime.Now:t}";
    }

    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
    }

    private async Task GenerateSystemReportAsync()
    {
        try
        {
            var deviceConfig = DeviceConfig.Instance;
            if (!deviceConfig.CanConnect)
            {
                UpdateUIMessage("Device not connected. Cannot retrieve device info.");
                return;
            }

            UpdateUIMessage("Retrieving device info...");

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
            };

            var commandBuilder = new StringBuilder();
            foreach (var (section, command) in systemCommands)
                commandBuilder.Append($"echo '=== {section} ===' && {command} && ");

            commandBuilder.Append("echo '=== OpenIPC Configurations ===' && ");

            foreach (var file in configFiles)
            {
                commandBuilder.Append($"echo '--- {file} ---' && ");
                commandBuilder.Append($"cat {file} 2>/dev/null || echo 'File not found' && ");
            }

            commandBuilder.Append("echo '=== Device Logs ===' && ");
            commandBuilder.Append("echo '--- Journal Logs (Last 100 Entries) ---' && ");
            commandBuilder.Append("journalctl -n 100 2>/dev/null || echo 'journalctl not available' && ");
            commandBuilder.Append("echo -e '\\n\\n--- OpenIPC readlog Output ---' && ");
            commandBuilder.Append("logread 2>/dev/null || echo 'logread command not available' && ");
            commandBuilder.Append("echo -e '\\n\\n=== Network Diagnostics ===' && ");
            commandBuilder.Append("echo '--- Network Interfaces ---' && ");
            commandBuilder.Append("ip addr && ");
            commandBuilder.Append("echo '--- Routing Table ---' && ");
            commandBuilder.Append("ip route && ");
            commandBuilder.Append("echo '--- DNS Configuration ---' && ");
            commandBuilder.Append("cat /etc/resolv.conf && ");
            commandBuilder.Append("echo '--- Ping Test (Google DNS) ---' && ");
            commandBuilder.Append("ping -c 4 8.8.8.8 || echo 'Ping failed' && ");
            commandBuilder.Append("echo '--- Wireless Interfaces ---' && ");
            commandBuilder.Append("iwconfig 2>/dev/null || echo 'iwconfig not available' && ");
            commandBuilder.Append("echo '--- Wireless Status ---' && ");
            commandBuilder.Append("iwconfig wlan0 2>/dev/null || echo 'iwconfig wlan0 not available' && ");
            commandBuilder.Append("echo '--- Network Connection Stats ---' && ");
            commandBuilder.Append("netstat -tuln || echo 'netstat not available' && ");

            var reportCommands = commandBuilder.ToString();
            if (reportCommands.EndsWith(" && ", StringComparison.Ordinal))
                reportCommands = reportCommands[..^4];

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var result = await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig, reportCommands, cts.Token);

            if (result == null || string.IsNullOrEmpty(result.Result))
            {
                UpdateUIMessage("Failed to retrieve device info. No data received.");
                return;
            }

            await SaveContentAsync(
                "Save Device Info",
                $"device_info_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
                result.Result,
                "Device info");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to retrieve device info");
            UpdateUIMessage($"Error retrieving device info: {ex.Message}");
        }
    }

    private void OpenLogsFolder()
    {
        try
        {
            var logsDirectory = Path.Combine(OpenIPC.AppDataConfigDirectory, "Logs");
            Directory.CreateDirectory(logsDirectory);

            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = logsDirectory,
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{logsDirectory}\"",
                    UseShellExecute = true
                });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = logsDirectory,
                    UseShellExecute = true
                });
            }
            else
            {
                UpdateUIMessage($"Logs folder: {logsDirectory}");
                return;
            }

            UpdateUIMessage($"Opened logs folder: {logsDirectory}");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to open logs folder");
            UpdateUIMessage($"Error opening logs folder: {ex.Message}");
        }
    }

    private async Task SaveContentAsync(string title, string initialFileName, string content, string contentLabel)
    {
        UpdateUIMessage($"{contentLabel} ready. Select a location to save it...");

        var saveFileDialog = new SaveFileDialog
        {
            Title = title,
            DefaultExtension = "txt",
            InitialFileName = initialFileName,
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Text Files", Extensions = new List<string> { "txt" } },
                new() { Name = "All Files", Extensions = new List<string> { "*" } }
            }
        };

        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow == null)
        {
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                initialFileName);

            File.WriteAllText(defaultPath, content);
            UpdateUIMessage($"{contentLabel} saved to: {defaultPath}");
            return;
        }

        var filePath = await saveFileDialog.ShowAsync(mainWindow);
        if (string.IsNullOrEmpty(filePath))
        {
            UpdateUIMessage("Save operation canceled.");
            return;
        }

        File.WriteAllText(filePath, content);
        UpdateUIMessage($"{contentLabel} saved to: {filePath}");
    }

    private void OnAppMessage(AppMessage message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (CanConnect != message.CanConnect)
                CanConnect = message.CanConnect;

            OnPropertyChanged(nameof(CanConnect));
        });
    }
}
