using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using MsBox.Avalonia.Enums;
using Newtonsoft.Json.Linq;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Renci.SshNet.Messages;
using Serilog;
using SharpCompress.Archives;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing firmware updates and device configuration
/// </summary>
public partial class FirmwareTabViewModel : ViewModelBase
{
    private enum SysupgradePhase
    {
        None,
        UploadKernel,
        UploadRootfs,
        KernelFlash,
        RootfsFlash,
        OverlayErase,
        WaitingForOffline,
        WaitingForPing,
        WaitingForSsh
    }

    #region Private Fields

    private readonly HttpClient _httpClient;
    private readonly SysUpgradeService _sysupgradeService;
    private CancellationTokenSource _cancellationTokenSource;
    private FirmwareData _firmwareData;
    private readonly IGitHubService _gitHubService;
    private readonly IPreferencesService _preferencesService;
    private bool _bInitializedCommands = false;
    private bool _bRecursionSelectGuard = false;
    private readonly IMessageBoxService _messageBoxService;
    private SysupgradePhase _sysupgradePhase = SysupgradePhase.None;
    private bool _sysupgradeInProgress = false;
    private DispatcherTimer _flashTimer;
    private static readonly IBrush ProgressRunningBrush = Brushes.Green;
    private static readonly IBrush ProgressErrorBrush = Brushes.Red;
    private static readonly IBrush ProgressCompleteBrush = new SolidColorBrush(Color.Parse("#4C61D8"));

    private const int UploadKernelStart = 20;
    private const int UploadKernelEnd = 30;
    private const int UploadRootfsStart = 30;
    private const int UploadRootfsEnd = 40;
    private const int KernelFlashStart = 40;
    private const int KernelFlashEnd = 60;
    private const int RootfsFlashStart = 60;
    private const int RootfsFlashEnd = 90;
    private const int OverlayEraseStart = 90;
    private const int OverlayEraseEnd = 94;
    private const int WaitForOfflineStart = 94;
    private const int WaitForOfflineEnd = 95;
    private const int WaitForPingStart = 95;
    private const int WaitForPingEnd = 97;
    private const int WaitForSshStart = 97;
    private const int WaitForSshEnd = 99;

    private static readonly Regex AnsiEscapeRegex = new(@"\x1B\[[0-9;]*[mK]", RegexOptions.Compiled);
    private static readonly Regex AnsiBracketRegex = new(@"\[[0-9;]*m", RegexOptions.Compiled);
    private static readonly Regex FlashPercentRegex =
        new(@"(?:Erasing block|Writing kb|Verifying kb):.*\((?<percent>\d{1,3})%\)", RegexOptions.Compiled);
    private static readonly Regex OverlayPercentRegex =
        new(@"Erasing 64 Kibyte @ .* -\s*(?<percent>\d{1,3})%\s*complete",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RecoveryPercentRegex =
        new(@"Recovery progress:\s*(?<stage>offline|ping|ssh)\s+(?<percent>\d{1,3})%",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MtdLineRegex =
        new(@"^(?<dev>mtd\d+):\s+(?<size>[0-9a-fA-F]+)\s+(?<erasesize>[0-9a-fA-F]+)\s+""(?<name>[^""]+)""",
            RegexOptions.Compiled);
    private const string OpenIpcFirmwareSource = "OpenIPC Builder";
    private const string GregApfpvFirmwareSource = "Greg APFPV";
    #endregion

    #region Observable Properties

    [ObservableProperty] private bool _canConnect;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isLocalFirmwarePackageSelected;
    [ObservableProperty] private bool _isFirmwareBySocSelected;
    [ObservableProperty] private bool _isManufacturerDeviceFirmwareComboSelected;
    [ObservableProperty] private bool _canDownloadFirmware;
    [ObservableProperty] private bool _isManualUpdateEnabled;
    [ObservableProperty] private string _selectedDevice;
    [ObservableProperty] private string _selectedFirmware;
    [ObservableProperty] private string _selectedFirmwareBySoc;
    [ObservableProperty] private string _selectedManufacturer;
    [ObservableProperty] private string _manualLocalFirmwarePackageFile;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private IBrush _progressBarBrush = new SolidColorBrush(Color.Parse("#4C61D8"));
    [ObservableProperty] private string _selectedBootloader;
    [ObservableProperty] private bool _bootloaderConfirmed;
    [ObservableProperty] private int _bootloaderProgressValue;
    [ObservableProperty] private string _bootloaderProgressText;
    [ObservableProperty] private bool _bootloaderInProgress;
    [ObservableProperty] private string _bootloaderStorageTypeLabel = "Detected storage: Unknown";
    [ObservableProperty] private bool _firmwareUpgradeInProgress;
    [ObservableProperty] private bool _backupInProgress;
    [ObservableProperty] private int _backupProgressValue;
    [ObservableProperty] private string _backupProgressText;
    [ObservableProperty] private bool _restoreInProgress;
    [ObservableProperty] private int _restoreProgressValue;
    [ObservableProperty] private string _restoreProgressText;
    [ObservableProperty] private bool _restoreConfirmed;
    [ObservableProperty] private bool _flashWarning;
    [ObservableProperty] private bool _isBackupExpanded;
    [ObservableProperty] private bool _isFirmwareExpanded = true;
    [ObservableProperty] private bool _isBootloaderExpanded;
    [ObservableProperty] private string _selectedFirmwareSource;
    [ObservableProperty] private int _selectedFirmwareMethod; // 0=Automatic, 1=BySoc, 2=Local

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets whether dropdowns should be enabled based on connection and firmware selection state
    /// </summary>
    public bool CanUseDropdowns => IsConnected && !IsGregApfpvSourceSelected();

    /// <summary>
    /// Gets whether soc dropdowns should be enabled based on connection and firmware selection state
    /// </summary>
    public bool CanUseDropdownsBySoc => IsConnected;

    
    /// <summary>
    /// Gets whether firmware selection is available based on connection and manufacturer selection state
    /// </summary>
    public bool CanUseSelectLocalFirmwarePackage => IsConnected;

    /// <summary>
    /// Collection of available manufacturers
    /// </summary>
    public ObservableCollection<string> Manufacturers { get; set; } = new();

    /// <summary>
    /// Collection of available devices for selected manufacturer
    /// </summary>
    public ObservableCollection<string> Devices { get; set; } = new();

    /// <summary>
    /// Collection of available firmware versions
    /// </summary>
    public ObservableCollection<string> Firmwares { get; set; } = new();

    /// <summary>
    /// Collection of available firmware versions
    /// </summary>
    public ObservableCollection<string> FirmwareBySoc { get; set; } = new();
    public ObservableCollection<string> Bootloaders { get; set; } = new();
    public ObservableCollection<string> FirmwareSources { get; } = new()
    {
        OpenIpcFirmwareSource,
        GregApfpvFirmwareSource
    };

    #endregion

    #region Commands

    public IAsyncRelayCommand<Window> SelectLocalFirmwarePackageCommand { get; set; }
    public IAsyncRelayCommand PerformFirmwareUpgradeAsyncCommand { get; set; }
    public IAsyncRelayCommand ReplaceBootloaderAsyncCommand { get; set; }
    public IAsyncRelayCommand BackupFirmwareAsyncCommand { get; set; }
    public IAsyncRelayCommand RestoreFirmwareAsyncCommand { get; set; }
    public ICommand ClearFormCommand { get; set; }
    public IAsyncRelayCommand DownloadFirmwareAsyncCommand { get; set; }

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of FirmwareTabViewModel
    /// </summary>
    public FirmwareTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IGitHubService gitHubService,
        IPreferencesService preferencesService,
        IMessageBoxService messageBoxService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _gitHubService = gitHubService;
        _preferencesService = preferencesService;
        _messageBoxService = messageBoxService;
        _httpClient = new HttpClient();
        _sysupgradeService = new SysUpgradeService(sshClientService, logger);
        _bInitializedCommands = false;
        _bRecursionSelectGuard = false;
        InitializeProperties();
        InitializeCommands();
        SubscribeToEvents();
    }

    #endregion

    #region Initialization Methods

    private void InitializeProperties()
    {
        CanConnect = false;
        IsConnected = false;
        IsLocalFirmwarePackageSelected = false;
        IsManufacturerDeviceFirmwareComboSelected = false;
        BootloaderConfirmed = false;
        BootloaderProgressValue = 0;
        BootloaderProgressText = string.Empty;
        BootloaderInProgress = false;
        BackupInProgress = false;
        BackupProgressValue = 0;
        BackupProgressText = string.Empty;
        RestoreInProgress = false;
        RestoreProgressValue = 0;
        RestoreProgressText = string.Empty;
        RestoreConfirmed = false;
        IsBackupExpanded = false;
        FirmwareUpgradeInProgress = false;
        IsFirmwareExpanded = true;
        IsBootloaderExpanded = false;
        var preferences = _preferencesService.Load();
        SelectedFirmwareSource = string.Equals(preferences.PreferredFirmwareSource, GregApfpvFirmwareSource,
            StringComparison.OrdinalIgnoreCase)
            ? GregApfpvFirmwareSource
            : OpenIpcFirmwareSource;
    }

    partial void OnIsFirmwareExpandedChanged(bool value)
    {
        if (!value)
            return;

        if (IsBootloaderExpanded)
            IsBootloaderExpanded = false;

        if (IsBackupExpanded)
            IsBackupExpanded = false;
    }

    partial void OnIsBootloaderExpandedChanged(bool value)
    {
        if (!value)
            return;

        if (IsFirmwareExpanded)
            IsFirmwareExpanded = false;

        if (IsBackupExpanded)
            IsBackupExpanded = false;
    }

    partial void OnIsBackupExpandedChanged(bool value)
    {
        if (!value)
            return;

        if (IsFirmwareExpanded)
            IsFirmwareExpanded = false;

        if (IsBootloaderExpanded)
            IsBootloaderExpanded = false;
    }

    private void InitializeCommands()
    {
        if (_bInitializedCommands)
            return;
        _bInitializedCommands = true;
        DownloadFirmwareAsyncCommand = new AsyncRelayCommand(
            DownloadAndPerformFirmwareUpgradeAsync,
            CanExecuteDownloadFirmware);

        PerformFirmwareUpgradeAsyncCommand = new AsyncRelayCommand(
            DownloadAndPerformFirmwareUpgradeAsync);

        ReplaceBootloaderAsyncCommand = new AsyncRelayCommand(
            ReplaceBootloaderAsync,
            CanExecuteReplaceBootloader);

        BackupFirmwareAsyncCommand = new AsyncRelayCommand(
            BackupFirmwareAsync,
            CanExecuteBackupFirmware);

        RestoreFirmwareAsyncCommand = new AsyncRelayCommand(
            RestoreFirmwareAsync,
            CanExecuteRestoreFirmware);

        SelectLocalFirmwarePackageCommand = new AsyncRelayCommand<Window>(
            SelectLocalFirmwarePackage);

        ClearFormCommand = new RelayCommand(ClearForm);
    }

    private void StartFlashTimer()
    {
        if (_flashTimer != null) return;
        _flashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _flashTimer.Tick += (_, _) => FlashWarning = !FlashWarning;
        _flashTimer.Start();
    }

    private void StopFlashTimer()
    {
        if (_flashTimer == null) return;
        _flashTimer.Stop();
        _flashTimer = null;
        FlashWarning = false;
    }

    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
    }

    #endregion

    #region Event Handlers

    private void OnAppMessage(AppMessage message)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => HandleAppMessage(message));
            return;
        }

        HandleAppMessage(message);
    }

    private void HandleAppMessage(AppMessage message)
    {
        CanConnect = message.CanConnect;
        IsConnected = message.CanConnect;

        _ = LoadManufacturersAsync();
        _ = LoadBootloadersAsync();

        if (!IsConnected)
        {
            IsLocalFirmwarePackageSelected = false;
            IsManufacturerDeviceFirmwareComboSelected = false;
            SelectedBootloader = string.Empty;
            BootloaderConfirmed = false;
            BootloaderProgressValue = 0;
            BootloaderProgressText = string.Empty;
            BootloaderInProgress = false;
            BackupProgressValue = 0;
            BackupProgressText = string.Empty;
            BackupInProgress = false;
            RestoreProgressValue = 0;
            RestoreProgressText = string.Empty;
            RestoreInProgress = false;
            RestoreConfirmed = false;
            FirmwareUpgradeInProgress = false;
        }

        UpdateCanExecuteCommands();
    }

    partial void OnSelectedFirmwareBySocChanged(string value)
    {
        IsFirmwareBySocSelected = !string.IsNullOrEmpty(value);
        if (_bRecursionSelectGuard)
            return;
        _bRecursionSelectGuard = true;
        SelectedManufacturer = string.Empty;
        SelectedDevice = string.Empty;
        SelectedFirmware = string.Empty;
        IsManufacturerDeviceFirmwareComboSelected = false;
        ManualLocalFirmwarePackageFile = string.Empty;
        IsLocalFirmwarePackageSelected = false;
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnSelectedFirmwareMethodChanged(int value)
    {
        if (_bRecursionSelectGuard)
            return;

        _bRecursionSelectGuard = true;

        // Clear selections from other methods
        if (value != 0) // Not Automatic
        {
            SelectedManufacturer = string.Empty;
            SelectedDevice = string.Empty;
            SelectedFirmware = string.Empty;
            IsManufacturerDeviceFirmwareComboSelected = false;
        }

        if (value != 1) // Not BySoc
        {
            SelectedFirmwareBySoc = string.Empty;
            IsFirmwareBySocSelected = false;
        }

        if (value != 2) // Not Local
        {
            ManualLocalFirmwarePackageFile = string.Empty;
            IsLocalFirmwarePackageSelected = false;
        }

        _bRecursionSelectGuard = false;

        OnPropertyChanged(nameof(IsAutomaticMethodSelected));
        OnPropertyChanged(nameof(IsBySocMethodSelected));
        OnPropertyChanged(nameof(IsLocalMethodSelected));
        UpdateCanExecuteCommands();
    }

    public bool IsAutomaticMethodSelected => SelectedFirmwareMethod == 0;
    public bool IsBySocMethodSelected => SelectedFirmwareMethod == 1;
    public bool IsLocalMethodSelected => SelectedFirmwareMethod == 2;

    partial void OnSelectedFirmwareSourceChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || _bRecursionSelectGuard)
            return;

        var preferences = _preferencesService.Load();
        preferences.PreferredFirmwareSource = value;
        _preferencesService.Save(preferences);
        ClearFirmwareSelectionsAndCollections();
        _ = LoadManufacturersAsync();
    }
    
    partial void OnSelectedManufacturerChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;
        
        _bRecursionSelectGuard = true;
        // clear out any firmware by soc or manual selection
        SelectedFirmwareBySoc = string.Empty;
        IsFirmwareBySocSelected = false;
        ManualLocalFirmwarePackageFile = string.Empty;
        IsLocalFirmwarePackageSelected = false;
        SelectedDevice = string.Empty;
        SelectedFirmware = string.Empty;
        IsManufacturerDeviceFirmwareComboSelected = false;
        LoadDevices(value);
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnSelectedDeviceChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;
        
        _bRecursionSelectGuard = true;
        // clear out any firmware by soc or manual selection
        SelectedFirmwareBySoc = string.Empty;
        IsFirmwareBySocSelected = false;
        ManualLocalFirmwarePackageFile = string.Empty;
        IsLocalFirmwarePackageSelected = false;
        SelectedFirmware = string.Empty;
        IsManufacturerDeviceFirmwareComboSelected = false;
        LoadFirmwares(value);
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnSelectedFirmwareChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;
        
        _bRecursionSelectGuard = true;
        SelectedFirmwareBySoc = string.Empty;
        IsFirmwareBySocSelected = false;
        ManualLocalFirmwarePackageFile = string.Empty;
        IsLocalFirmwarePackageSelected = false;
        
        var manufacturer = _firmwareData?.Manufacturers
                .FirstOrDefault(m => ((m.Name == SelectedManufacturer) || (m.FriendlyName == SelectedManufacturer)));

        var device = manufacturer?.Devices
            .FirstOrDefault(d => ((d.Name == SelectedDevice) || (d.FriendlyName == SelectedDevice)));

        var firmware = device?.FirmwarePackages
            .FirstOrDefault(f => ((f.Name == SelectedFirmware) || (f.FriendlyName == SelectedFirmware)));

        var filename = string.Empty;


        if (!string.IsNullOrEmpty(manufacturer?.Name) &&
            !string.IsNullOrEmpty(device?.Name) &&
            !string.IsNullOrEmpty(firmware?.Name))
        {
            filename = firmware.PackageFile;
            IsManufacturerDeviceFirmwareComboSelected = true;
            IsLocalFirmwarePackageSelected = false;
            ManualLocalFirmwarePackageFile = string.Empty;
        }
        else
        {
            IsManufacturerDeviceFirmwareComboSelected = false;
        }

        if ((!string.IsNullOrEmpty(filename)) && FirmwareBySoc.Contains(filename))
        {
            SelectedFirmwareBySoc = filename;
            IsFirmwareBySocSelected = true;
        }
        else
        {
            SelectedFirmwareBySoc = "";
            IsFirmwareBySocSelected = false;
        }
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnManualLocalFirmwarePackageFileChanged(string value)
    {
        if (_bRecursionSelectGuard)
            return;

        _bRecursionSelectGuard = true;
        if (!string.IsNullOrEmpty(value))
        {
            SelectedManufacturer = string.Empty;
            SelectedDevice = string.Empty;
            SelectedFirmware = string.Empty;
            SelectedFirmwareBySoc = string.Empty;
            IsFirmwareBySocSelected = false;
            IsManufacturerDeviceFirmwareComboSelected = false;
        }

        IsLocalFirmwarePackageSelected = !string.IsNullOrEmpty(value);
        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    partial void OnSelectedBootloaderChanged(string value)
    {
        BootloaderConfirmed = false;
        UpdateCanExecuteCommands();
    }

    partial void OnBootloaderConfirmedChanged(bool value)
    {
        UpdateCanExecuteCommands();
    }

    partial void OnBootloaderInProgressChanged(bool value)
    {
        UpdateCanExecuteCommands();
    }

    partial void OnFirmwareUpgradeInProgressChanged(bool value)
    {
        UpdateCanExecuteCommands();
    }

    partial void OnBackupInProgressChanged(bool value)
    {
        UpdateCanExecuteCommands();
    }

    partial void OnRestoreInProgressChanged(bool value)
    {
        UpdateCanExecuteCommands();
    }

    partial void OnRestoreConfirmedChanged(bool value)
    {
        UpdateCanExecuteCommands();
    }

    #endregion

    #region Public Methods

    public async Task LoadManufacturersAsync()
    {
        try
        {
            Logger.Information("Loading firmware list...");
            var data = await FetchFirmwareListAsync();
            UpdateManufacturers(data);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading manufacturers.");
        }
    }

    public async Task LoadBootloadersAsync()
    {
        try
        {
            Logger.Information("Loading bootloader list...");
            var filenames = await GetBootloaderFilenamesAsync();
            var detectedStorageType = await GetDeviceStorageTypeAsync();
            var storageType = NormalizeBootloaderStorageType(detectedStorageType);
            UpdateBootloaderStorageTypeLabel(detectedStorageType, storageType);
            var filtered = FilterBootloadersByStorage(filenames, storageType);
            UpdateBootloaders(filtered);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading bootloader list.");
        }
    }

    private void UpdateManufacturers(FirmwareData data)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateManufacturers(data));
            return;
        }

        Manufacturers.Clear();

        if (data?.Manufacturers != null && data.Manufacturers.Any())
        {
            foreach (var manufacturer in data.Manufacturers)
            {
                var hasValidFirmwareType = manufacturer.Devices.Any(device =>
                    device.FirmwarePackages.Any(firmware =>
                        DevicesFriendlyNames.FirmwareIsSupported(firmware.Name)));

                if (hasValidFirmwareType)
                    Manufacturers.Add(manufacturer.FriendlyName);
            }

            if (!Manufacturers.Any())
                Logger.Warning("No manufacturers with valid firmware types found.");
        }
        else
        {
            Logger.Warning("No manufacturers found in the fetched firmware data.");
        }
    }

    public void LoadDevices(string manufacturer)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => LoadDevices(manufacturer));
            return;
        }

        Devices.Clear();

        if (string.IsNullOrEmpty(manufacturer))
        {
            Logger.Warning("Manufacturer is null or empty. Devices cannot be loaded.");
            return;
        }

        manufacturer = DevicesFriendlyNames.ManufacturerByFriendlyName(manufacturer);

        var manufacturerData = _firmwareData?.Manufacturers
            .FirstOrDefault(m => m.Name == manufacturer);

        if (manufacturerData == null || !manufacturerData.Devices.Any())
        {
            Logger.Warning($"No devices found for manufacturer: {manufacturer}");
            return;
        }

        foreach (var device in manufacturerData.Devices)
            Devices.Add(device.FriendlyName);

        Logger.Information($"Loaded {Devices.Count} devices for manufacturer: {manufacturer}");
    }

    
    public void LoadFirmwares(string device)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => LoadFirmwares(device));
            return;
        }

        Firmwares.Clear();

        if (string.IsNullOrEmpty(device))
        {
            Logger.Warning("Device is null or empty. Firmwares cannot be loaded.");
            return;
        }

        device = DevicesFriendlyNames.DeviceByFriendlyName(device);
       
        var deviceData = _firmwareData?.Manufacturers
            .FirstOrDefault(m => ((m.Name == SelectedManufacturer)||(m.FriendlyName == SelectedManufacturer)))?.Devices
            .FirstOrDefault(d => d.Name == device);

        if (deviceData == null || !deviceData.FirmwarePackages.Any())
        {
            Logger.Warning($"No firmware found for device: {device}");
            return;
        }

        foreach (var firmware in deviceData.FirmwarePackages)
        {
           var firmwareName = DevicesFriendlyNames.FirmwareFriendlyNameById(firmware.Name);
           if (!Firmwares.Contains(firmwareName))
                Firmwares.Add(firmwareName);
        }

        Logger.Information($"Loaded {Firmwares.Count} firmware types for device: {device}");
    }

    #endregion

    #region Private Methods

    private void ClearForm()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ClearForm);
            return;
        }

        SelectedManufacturer = string.Empty;
        SelectedDevice = string.Empty;
        SelectedFirmware = string.Empty;
        SelectedFirmwareBySoc = string.Empty;
        ManualLocalFirmwarePackageFile = string.Empty;

        IsLocalFirmwarePackageSelected = false;
        IsManufacturerDeviceFirmwareComboSelected = false;
        IsManualUpdateEnabled = true;
        SelectedFirmwareMethod = 0;

        UpdateCanExecuteCommands();
    }

    private void ClearFirmwareSelectionsAndCollections()
    {
        _bRecursionSelectGuard = true;

        SelectedManufacturer = string.Empty;
        SelectedDevice = string.Empty;
        SelectedFirmware = string.Empty;
        SelectedFirmwareBySoc = string.Empty;
        ManualLocalFirmwarePackageFile = string.Empty;

        IsLocalFirmwarePackageSelected = false;
        IsManufacturerDeviceFirmwareComboSelected = false;
        IsManualUpdateEnabled = true;
        IsFirmwareBySocSelected = false;

        Manufacturers.Clear();
        Devices.Clear();
        Firmwares.Clear();
        FirmwareBySoc.Clear();

        _bRecursionSelectGuard = false;
        UpdateCanExecuteCommands();
    }

    private bool CanExecuteDownloadFirmware()
    {
        return CanConnect &&
               !BootloaderInProgress &&
               ((!string.IsNullOrEmpty(SelectedManufacturer) &&
                 !string.IsNullOrEmpty(SelectedDevice) &&
                 !string.IsNullOrEmpty(SelectedFirmware)) ||
                !string.IsNullOrEmpty(ManualLocalFirmwarePackageFile) ||
                !string.IsNullOrEmpty(SelectedFirmwareBySoc));
    }

    private void UpdateCanExecuteCommands()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(UpdateCanExecuteCommands);
            return;
        }

        CanDownloadFirmware = CanExecuteDownloadFirmware();

        OnPropertyChanged(nameof(CanUseDropdowns));
        OnPropertyChanged(nameof(CanUseDropdownsBySoc));
        OnPropertyChanged(nameof(CanUseSelectLocalFirmwarePackage));
        OnPropertyChanged(nameof(CanReplaceBootloader));
        OnPropertyChanged(nameof(CanBackupFirmware));
        OnPropertyChanged(nameof(CanRestoreFirmware));
        OnPropertyChanged(nameof(IsDestructiveOperationInProgress));
        OnPropertyChanged(nameof(DestructiveOperationWarning));
        OnPropertyChanged(nameof(IsAutomaticMethodSelected));

        if (IsDestructiveOperationInProgress)
            StartFlashTimer();
        else
            StopFlashTimer();
        OnPropertyChanged(nameof(IsBySocMethodSelected));
        OnPropertyChanged(nameof(IsLocalMethodSelected));

        if (IsConnected)
        {
            InitializeCommands();
            DownloadFirmwareAsyncCommand?.NotifyCanExecuteChanged();
            PerformFirmwareUpgradeAsyncCommand?.NotifyCanExecuteChanged();
            ReplaceBootloaderAsyncCommand?.NotifyCanExecuteChanged();
            BackupFirmwareAsyncCommand?.NotifyCanExecuteChanged();
            RestoreFirmwareAsyncCommand?.NotifyCanExecuteChanged();
            SelectLocalFirmwarePackageCommand?.NotifyCanExecuteChanged();
        }
    }

    public bool CanReplaceBootloader =>
        IsConnected &&
        !BootloaderInProgress &&
        !BackupInProgress &&
        !FirmwareUpgradeInProgress &&
        !string.IsNullOrWhiteSpace(SelectedBootloader) &&
        BootloaderConfirmed;

    public bool CanBackupFirmware =>
        IsConnected &&
        !BootloaderInProgress &&
        !FirmwareUpgradeInProgress &&
        !BackupInProgress &&
        !RestoreInProgress;

    public bool CanRestoreFirmware =>
        IsConnected &&
        !BootloaderInProgress &&
        !FirmwareUpgradeInProgress &&
        !BackupInProgress &&
        !RestoreInProgress &&
        RestoreConfirmed;

    public bool IsDestructiveOperationInProgress =>
        BackupInProgress || RestoreInProgress || FirmwareUpgradeInProgress || BootloaderInProgress;

    public string DestructiveOperationWarning
    {
        get
        {
            if (RestoreInProgress) return "DO NOT INTERRUPT — RESTORING FIRMWARE — DO NOT POWER OFF";
            if (BootloaderInProgress) return "DO NOT INTERRUPT — FLASHING BOOTLOADER — DO NOT POWER OFF";
            if (FirmwareUpgradeInProgress) return "DO NOT INTERRUPT — FIRMWARE UPGRADE IN PROGRESS — DO NOT POWER OFF";
            if (BackupInProgress) return "DO NOT INTERRUPT — BACKUP IN PROGRESS";
            return string.Empty;
        }
    }

    private async Task<FirmwareData> FetchFirmwareListAsync()
    {
        try
        {
            Logger.Information("Fetching firmware list...");

            IEnumerable<string> filenames = await GetFilenamesAsync();

            Logger.Information($"Fetched {filenames.Count()} firmware files.");

            FirmwareData firmwareData = IsGregApfpvSourceSelected()
                ? new FirmwareData { Manufacturers = new ObservableCollection<Manufacturer>() }
                : ProcessFilenames(filenames);
            
            // Populate FirmwareBySoc
            PopulateFirmwareBySoc(filenames); // Calling populate method here

            _firmwareData = firmwareData;
            return firmwareData;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error fetching firmware list.");
            return null;
        }
    }
    
    private void PopulateFirmwareBySoc(IEnumerable<string> filenames)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => PopulateFirmwareBySoc(filenames));
            return;
        }

        FirmwareBySoc.Clear(); // Clear existing list

        var chipType = DeviceConfig.Instance.ChipType;

        if (string.IsNullOrEmpty(chipType))
            return;
        
        foreach (var filename in filenames)
        {
            //string pattern = $@"^(?=.*{Regex.Escape(chipType)})(?=.*fpv).*?(?<memoryType>nand|nor)\.tgz$";  //Dynamically create regex with escaped chipType
            string simplePattern = $@".*{chipType}.*fpv.*";
            var match = Regex.Match(filename, simplePattern, RegexOptions.IgnoreCase); //Added RegexOptions.IgnoreCase to compare 
            if (match.Success)
            {
                if (!FirmwareBySoc.Contains(filename))
                {
                    FirmwareBySoc.Add(filename);
                    Logger.Information($"Added FirmwareBySoc: {filename}");
                }
            }
        }

        Logger.Information($"Populated FirmwareBySoc with {FirmwareBySoc.Count} entries.");
    }

    private async Task<IEnumerable<string>> GetFilenamesAsync()
    {
        if (IsGregApfpvSourceSelected())
        {
            var response = await _gitHubService.GetGitHubDataAsync(OpenIPC.GregApfpvContentsGitHubApiUrl);
            if (string.IsNullOrEmpty(response))
                return Enumerable.Empty<string>();

            var items = JArray.Parse(response);
            return items
                .Where(item => item["type"]?.ToString() == "file")
                .Select(item => item["name"]?.ToString())
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .Where(name => name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            var response = await _gitHubService.GetGitHubDataAsync(OpenIPC.OpenIPCBuilderGitHubApiUrl);
            if (string.IsNullOrEmpty(response))
                return Enumerable.Empty<string>();

            var releaseData = JObject.Parse(response);
            var assets = releaseData["assets"];
            return assets?.Select(asset => asset["name"]?.ToString()).Where(name => !string.IsNullOrEmpty(name)) ??
                   Enumerable.Empty<string>();
        }
    }

    private async Task<IEnumerable<string>> GetBootloaderFilenamesAsync()
    {
        var response = await _gitHubService.GetGitHubDataAsync(OpenIPC.OpenIPCFirmwareGitHubApiUrl);
        if (string.IsNullOrEmpty(response))
            return Enumerable.Empty<string>();

        var releaseData = JObject.Parse(response.ToString());
        var assets = releaseData["assets"];
        return assets?.Select(asset => asset["name"]?.ToString()).Where(name => !string.IsNullOrEmpty(name)) ??
               Enumerable.Empty<string>();
    }

    private static IEnumerable<string> FilterBootloadersByStorage(IEnumerable<string> filenames, string storageType)
    {
        var bootloaderFiles = filenames
            .Where(name => name.Contains("u-boot", StringComparison.OrdinalIgnoreCase) &&
                           name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!bootloaderFiles.Any())
            return bootloaderFiles;

        storageType = NormalizeBootloaderStorageType(storageType);

        var filtered = bootloaderFiles
            .Where(name => IsBootloaderForStorage(name, storageType))
            .ToList();

        return filtered.Any() ? filtered : bootloaderFiles;
    }

    private static bool IsBootloaderForStorage(string filename, string storageType)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        storageType = NormalizeBootloaderStorageType(storageType);
        var lower = filename.ToLowerInvariant();
        return storageType == "nor"
            ? (lower.Contains("-nor") || lower.Contains("_nor"))
            : (lower.Contains("-nand") || lower.Contains("_nand"));
    }

    private static string NormalizeBootloaderStorageType(string storageType)
    {
        return string.Equals(storageType, "nand", StringComparison.OrdinalIgnoreCase)
            ? "nand"
            : "nor";
    }

    private async Task<string> GetDeviceStorageTypeAsync()
    {
        if (!IsConnected)
            return null;

        try
        {
            var response = await SshClientService.ExecuteCommandWithResponseAsync(
                DeviceConfig.Instance,
                "cat /proc/cmdline",
                CancellationToken.None);
            var cmdline = response?.Result ?? string.Empty;

            if (cmdline.Contains("mtdparts=NOR_FLASH", StringComparison.OrdinalIgnoreCase) ||
                cmdline.Contains("NOR_FLASH", StringComparison.OrdinalIgnoreCase))
                return "nor";

            if (cmdline.Contains("mtdparts=NAND", StringComparison.OrdinalIgnoreCase) ||
                cmdline.Contains("NAND", StringComparison.OrdinalIgnoreCase))
                return "nand";
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to determine storage type from /proc/cmdline.");
        }

        return null;
    }

    private void UpdateBootloaderStorageTypeLabel(string detectedStorageType, string resolvedStorageType)
    {
        var label = "Detected storage: Unknown (defaulting to NOR)";
        if (string.Equals(detectedStorageType, "nor", StringComparison.OrdinalIgnoreCase))
            label = "Detected storage: NOR";
        else if (string.Equals(detectedStorageType, "nand", StringComparison.OrdinalIgnoreCase))
            label = "Detected storage: NAND";
        else if (string.Equals(resolvedStorageType, "nor", StringComparison.OrdinalIgnoreCase))
            label = "Detected storage: Unknown (defaulting to NOR)";

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => BootloaderStorageTypeLabel = label);
            return;
        }

        BootloaderStorageTypeLabel = label;
    }

    private async Task<long?> GetBootPartitionSizeAsync()
    {
        try
        {
            var response = await SshClientService.ExecuteCommandWithResponseAsync(
                DeviceConfig.Instance,
                "cat /proc/mtd",
                CancellationToken.None);
            var output = response?.Result ?? string.Empty;
            var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var match = MtdLineRegex.Match(line.Trim());
                if (!match.Success)
                    continue;

                var name = match.Groups["name"].Value.Trim().Trim('"');
                if (!name.Equals("boot", StringComparison.OrdinalIgnoreCase))
                    continue;

                var sizeHex = match.Groups["size"].Value;
                if (long.TryParse(sizeHex, System.Globalization.NumberStyles.HexNumber, null, out var size))
                    return size;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to read boot partition size from /proc/mtd.");
        }

        return null;
    }

    private FirmwareData ProcessFilenames(IEnumerable<string> filenames)
    {
        var firmwareData = new FirmwareData { Manufacturers = new ObservableCollection<Manufacturer>() };

        // First, parse and add canonical firmware files;
        // Second, parse add generic firmwares (as the can be added to multiple manufacturers as wildcards)

        foreach (var filename in filenames)
        {
            ProcessIndividualFirmwareFile(filename, firmwareData, true);
        }

        foreach (var filename in filenames)
        {
            ProcessIndividualFirmwareFile(filename, firmwareData, false);
        }

        if (firmwareData.Manufacturers.Count > 0)
        {
            // Add OpenIPC generic SSC338 FPV firmware to the generic entry too
            AddFirmwareData(firmwareData, "ssc338q_fpv_openipc-urllc-aio-nor.tgz", "*", "ssc338q", "urllc-aio", "ssc338q", "nor");

            // Add OpenIPC Thinker built in WiFi firmware as it's not part of the canonical list
            AddFirmwareData(firmwareData, "ssc338q_fpv_openipc-thinker-aio-nor.tgz", "openipc", "thinker-aio-wifi", "fpv", "ssc338q", "nor");
            firmwareData.SortCollections();
        }
        return firmwareData;
    }
    
    private void ProcessIndividualFirmwareFile(string firmwarePackageFilename, FirmwareData firmwareData, bool bCanonicalFilesOnly)
    {
        // A firmware file can be:
        //  * For a particular SOC and a particular manufacturer
        //  * For a particular SOC but any manufacturer
        //  * For a particular SOC and a particular HW configuration (ie.Thinker Wifi)
        // The common firmware filename convention is for first case above, with the filename format: SOC_firmwaretype_manufacturer-device-memorytype
        // For the other cases, different filename conventions could be used (ie: for a firmware generic for all manufacturers: firmwaretype_SOC)
        // Filenames must be canonized after a match is found


        var match = Regex.Match(firmwarePackageFilename,
            @"^(?<sensor>[^_]+)_(?<firmwareType>[^_]+)_(?<manufacturer>[^-]+)-(?<device>.+?)-(?<memoryType>nand|nor)");
        
        if (firmwarePackageFilename.StartsWith("ssc338q_rubyfpv") && (!bCanonicalFilesOnly) )
        {
            ProcessFirmwareMatchGenericRuby(firmwarePackageFilename, firmwareData);
        }
        else if (match.Success && (!firmwarePackageFilename.Contains("rubyfpv")) && bCanonicalFilesOnly )
        {
            ProcessFirmwareMatchForManufacturer(firmwarePackageFilename, match, firmwareData);
        }
        else
        {
            Debug.WriteLine($"Filename '{firmwarePackageFilename}' does not match the expected pattern.");
        }
    }

    private void ProcessFirmwareMatchGenericRuby(string firmwarePackageFilename, FirmwareData firmwareData)
    {
        string socType = "ssc338q";
        string firmwareType = "rubyfpv";
        string manufacturerName = "*";
        string deviceName = "ssc338q";
        string memoryType = "nor";

        if (firmwarePackageFilename.Contains("thinker_internal") )
        {
            manufacturerName = "openipc";
            deviceName = "thinker-aio-wifi";
        }

        if (DeviceConfig.Instance.ChipType != socType)
            return; // using `return` to exit the method. continue is no longer relevant here

        Debug.WriteLine(
            $"Parsed file: SOCType={socType}, FirmwareType={firmwareType}, Manufacturer={manufacturerName}, Device={deviceName}, MemoryType={memoryType}");

        AddFirmwareData(firmwareData, firmwarePackageFilename, manufacturerName, deviceName, firmwareType, socType, memoryType);

        // Add this generic firmware to all supported manufacturers and devices, including generic OpenIPC device
        if (manufacturerName.Equals("*"))
        {
            foreach (var manufacturer in firmwareData.Manufacturers)
            {
                foreach (var device in manufacturer.Devices)
                {
                    AddFirmwareData(firmwareData, firmwarePackageFilename, manufacturer.Name, device.Name, firmwareType, socType, memoryType);
                }
            }
        }
    }

    private void ProcessFirmwareMatchForManufacturer(string firmwarePackageFilename, Match match, FirmwareData firmwareData)
    {
        var socType = match.Groups["sensor"].Value;

        // only show firmware that matches the selected sensor/soc
        if (DeviceConfig.Instance.ChipType != socType)
            return; // using `return` to exit the method. continue is no longer relevant here

        var firmwareType = match.Groups["firmwareType"].Value;
        var manufacturerName = match.Groups["manufacturer"].Value;
        var deviceName = match.Groups["device"].Value;
        var memoryType = match.Groups["memoryType"].Value;

        Debug.WriteLine(
            $"Parsed file: SOCType={socType}, FirmwareType={firmwareType}, Manufacturer={manufacturerName}, Device={deviceName}, MemoryType={memoryType}");

        AddFirmwareData(firmwareData, firmwarePackageFilename, manufacturerName, deviceName, firmwareType, socType, memoryType);
    }

    private void AddFirmwareData(FirmwareData firmwareData, string firmwarePackageFilename, string manufacturerName, string deviceName,
        string firmwareType, string socType, string memoryType)
    {
        var manufacturer = firmwareData.Manufacturers.FirstOrDefault(m => m.Name == manufacturerName);
        if (manufacturer == null)
        {
            manufacturer = CreateAndAddManufacturer(firmwareData, manufacturerName);
        }

        var device = manufacturer.Devices.FirstOrDefault(d => d.Name == deviceName);
        if ( device == null )
        {
            device = CreateAndAddDevice(manufacturer, deviceName);
        }


        var firmwarePackage = device.FirmwarePackages.FirstOrDefault(f => f.PackageFile == firmwarePackageFilename);
        if ( firmwarePackage == null )
        {
            firmwarePackage = CreateAndAddFirmwarePackage(manufacturer, device, firmwarePackageFilename, firmwareType);
        }
    
    }
    
    private Manufacturer CreateAndAddManufacturer(FirmwareData firmwareData, string manufacturerName)
    {
        var manufacturer = new Manufacturer
        {
            Name = manufacturerName,
            FriendlyName = DevicesFriendlyNames.ManufacturerFriendlyNameById(manufacturerName),
            Devices = new ObservableCollection<Device>()
        };
        firmwareData.Manufacturers.Add(manufacturer);
        return manufacturer;
    }

    private Device CreateAndAddDevice(Manufacturer manufacturer, string deviceName)
    {
        var device = new Device
        {
            Name = deviceName,
            FriendlyName = DevicesFriendlyNames.DeviceFriendlyNameById(deviceName),
            FirmwarePackages = new ObservableCollection<FirmwarePackage>()
        };
        manufacturer.Devices.Add(device);
        return device;
    }

    private FirmwarePackage CreateAndAddFirmwarePackage(Manufacturer manufacturer, Device device, string firmwarePackageFilename, string firmwareType)
    {
        var firmwarePackage = new FirmwarePackage
        {
            Name = firmwareType,
            FriendlyName = DevicesFriendlyNames.FirmwareFriendlyNameById(firmwareType),
            PackageFile = firmwarePackageFilename
        };
        
        device.FirmwarePackages.Add(firmwarePackage);
        return firmwarePackage;
    }

    private async Task<string> DownloadFirmwareAsync(string url = null, string filename = null)
    {
        try
        {
            var filePath = Path.Combine(Path.GetTempPath(), filename);
            Logger.Information($"Downloading firmware from {url} to {filePath}");

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

                Logger.Information($"Firmware successfully downloaded to: {filePath}");
                return filePath;
            }
            else
            {
                Logger.Warning($"Failed to download firmware. Status code: {response.StatusCode}");
                ProgressValue = 100;
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error downloading firmware");
            return null;
        }
    }

    private async Task<string> DownloadBootloaderAsync(string filename)
    {
        try
        {
            var filePath = Path.Combine(OpenIPC.LocalTempFolder, filename);
            Directory.CreateDirectory(OpenIPC.LocalTempFolder);
            var url = $"https://github.com/OpenIPC/firmware/releases/download/latest/{filename}";

            Logger.Information($"Downloading bootloader from {url} to {filePath}");

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await response.Content.CopyToAsync(fs);

                Logger.Information($"Bootloader successfully downloaded to: {filePath}");
                return filePath;
            }

            Logger.Warning($"Failed to download bootloader. Status code: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error downloading bootloader");
            return null;
        }
    }

    private void UpdateBootloaders(IEnumerable<string> filenames)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateBootloaders(filenames));
            return;
        }

        Bootloaders.Clear();

        var bootloaderFiles = filenames.ToList();

        if (!bootloaderFiles.Any())
        {
            Logger.Warning("No bootloader files found in firmware release assets.");
            return;
        }

        var chipType = DeviceConfig.Instance.ChipType;
        if (!string.IsNullOrEmpty(chipType))
        {
            var chipMatches = bootloaderFiles
                .Where(name => name.Contains(chipType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (chipMatches.Any())
                bootloaderFiles = chipMatches;
        }

        foreach (var bootloader in bootloaderFiles)
            Bootloaders.Add(bootloader);

        if (Bootloaders.Any() && string.IsNullOrEmpty(SelectedBootloader))
            SelectedBootloader = Bootloaders.FirstOrDefault();

        Logger.Information($"Loaded {Bootloaders.Count} bootloader entries.");
    }

    private string DecompressTgzToTar(string tgzFilePath)
    {
        try
        {
            var tarFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(tgzFilePath)}.tar");

            using (var fileStream = File.OpenRead(tgzFilePath))
            using (var gzipStream =
                   new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress))
            using (var tarFileStream = File.Create(tarFilePath))
            {
                gzipStream.CopyTo(tarFileStream);
            }

            Logger.Information($"Decompressed .tgz to .tar: {tarFilePath}");
            return tarFilePath;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error decompressing .tgz file");
            throw;
        }
    }

    private string UncompressFirmware(string tgzFilePath)
    {
        try
        {
            var tarFilePath = DecompressTgzToTar(tgzFilePath);

            ProgressValue = 4;
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(tgzFilePath));
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);

            Directory.CreateDirectory(tempDir);

            ProgressValue = 8;
            using (var archive = SharpCompress.Archives.Tar.TarArchive.Open(tarFilePath))
            {
                foreach (var entry in archive.Entries.Where(entry => !entry.IsDirectory))
                {
                    var destinationPath = Path.Combine(tempDir, entry.Key);
                    var directoryPath = Path.GetDirectoryName(destinationPath);

                    if (!Directory.Exists(directoryPath))
                        Directory.CreateDirectory(directoryPath);

                    using (var entryStream = entry.OpenEntryStream())
                    using (var fileStream = File.Create(destinationPath))
                    {
                        entryStream.CopyTo(fileStream);
                    }
                }
            }

            Logger.Information($"Firmware extracted to {tempDir}");
            return tempDir;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error uncompressing firmware file");
            throw;
        }
    }

    private void ValidateFirmwareFiles(string extractedDir)
    {
        try
        {
            var allFiles = Directory.GetFiles(extractedDir);

            var md5Files = allFiles.Where(file => file.EndsWith(".md5sum")).ToList();

            if (!md5Files.Any())
                throw new InvalidOperationException("No MD5 checksum files found in the extracted directory.");

            foreach (var md5File in md5Files)
            {
                var baseFileName = Path.GetFileNameWithoutExtension(md5File);

                var dataFile = allFiles.FirstOrDefault(file => Path.GetFileName(file) == baseFileName);
                if (dataFile == null)
                    throw new FileNotFoundException(
                        $"Data file '{baseFileName}' referenced by '{md5File}' is missing.");

                ValidateMd5Checksum(md5File, dataFile);
            }

            Logger.Information("Firmware files validated successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error validating firmware files");
            throw;
        }
    }

    private void ValidateMd5Checksum(string md5FilePath, string dataFilePath)
    {
        try
        {
            var md5Line = File.ReadAllText(md5FilePath).Trim();

            var parts = md5Line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException($"Invalid format in MD5 file: {md5FilePath}");
            }

            var expectedMd5 = parts[0];
            var expectedFilename = parts[1];

            if (Path.GetFileName(dataFilePath) != expectedFilename)
            {
                throw new InvalidOperationException(
                    $"Filename mismatch: expected '{expectedFilename}', found '{Path.GetFileName(dataFilePath)}'");
            }

            using var md5 = System.Security.Cryptography.MD5.Create();
            using var stream = File.OpenRead(dataFilePath);
            var actualMd5 = BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();

            if (expectedMd5 != actualMd5)
            {
                throw new InvalidOperationException(
                    $"MD5 mismatch for file: {Path.GetFileName(dataFilePath)}. Expected: {expectedMd5}, Actual: {actualMd5}");
            }

            Logger.Information($"File '{Path.GetFileName(dataFilePath)}' passed MD5 validation.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Error validating MD5 checksum for file: {dataFilePath}");
            throw;
        }
    }

    private async Task DownloadAndPerformFirmwareUpgradeAsync()
    {
        if (BootloaderInProgress)
            return;

        FirmwareUpgradeInProgress = true;
        try
        {
            ProgressValue = 0;

            if (!string.IsNullOrEmpty(ManualLocalFirmwarePackageFile))
            {
                Logger.Information("Performing firmware upgrade using manual file.");
                await UpgradeFirmwareFromFileAsync(ManualLocalFirmwarePackageFile);
            }
            else if (!string.IsNullOrEmpty(SelectedFirmwareBySoc))
            {
                Logger.Information("Performing firmware upgrade using firmware by soc.");
                await PerformFirmwareUpgradeFromSocAsync();
            }
            else
            {
                if (string.IsNullOrEmpty(SelectedManufacturer) ||
                    string.IsNullOrEmpty(SelectedDevice) ||
                    string.IsNullOrEmpty(SelectedFirmware))
                {
                    Logger.Warning("Cannot perform firmware upgrade. Missing dropdown selections.");
                    return;
                }

                Logger.Information("Performing firmware upgrade using selected dropdown options.");
                await PerformFirmwareUpgradeFromDropdownAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing firmware upgrade");
        }
        finally
        {
            FirmwareUpgradeInProgress = false;
        }
    }

    private bool CanExecuteReplaceBootloader()
    {
        return CanReplaceBootloader;
    }

    private bool CanExecuteBackupFirmware()
    {
        return CanBackupFirmware;
    }

    private bool CanExecuteRestoreFirmware()
    {
        return CanRestoreFirmware;
    }

    private async Task BackupFirmwareAsync()
    {
        var deviceConfig = DeviceConfig.Instance;
        if (!deviceConfig.CanConnect)
            return;

        var chipType = string.IsNullOrWhiteSpace(deviceConfig.ChipType) ? "unknown-soc" : deviceConfig.ChipType;
        var sanitizedChipType = Regex.Replace(chipType.ToLowerInvariant(), @"[^a-z0-9._-]+", "-");
        var backupName = $"backup-{sanitizedChipType}-{DateTime.Now:yyyyMMdd-HHmmss}";
        var remoteBaseDir = $"{OpenIPC.RemoteTempFolder}/mtd-backup";
        var remoteBackupDir = $"{remoteBaseDir}/{backupName}";
        var remoteArchivePath = $"{OpenIPC.RemoteTempFolder}/{backupName}.tar.gz";

        try
        {
            BackupInProgress = true;
            BackupProgressValue = 5;
            BackupProgressText = "Stopping services on device...";
            UpdateCanExecuteCommands();

            await SshClientService.ExecuteCommandAsync(
                deviceConfig,
                "killall -q -3 majestic; killall -q wfb_tx; killall -q wfb_rx; " +
                "killall -q msposd; killall -q telemetry_rx; killall -q telemetry_tx; " +
                "sleep 1; sync; echo 3 > /proc/sys/vm/drop_caches; true");

            BackupProgressValue = 8;
            BackupProgressText = "Preparing backup directory on device...";
            await SshClientService.ExecuteCommandAsync(
                deviceConfig,
                $"rm -rf '{remoteBaseDir}' '{remoteArchivePath}' && mkdir -p '{remoteBackupDir}'");

            BackupProgressValue = 12;
            BackupProgressText = "Backing up all MTD partitions on device...";
            await SshClientService.ExecuteCommandAsync(
                deviceConfig,
                $"for mtd in $(ls /dev/mtdblock*); do dd if=${{mtd}} of='{remoteBackupDir}'/${{mtd##/*/}}.bin; done");

            BackupProgressValue = 70;
            BackupProgressText = "Syncing and generating checksum file on device...";
            await SshClientService.ExecuteCommandAsync(
                deviceConfig,
                $"sync && cd '{remoteBackupDir}' && md5sum mtdblock*.bin > md5sums.txt");

            BackupProgressValue = 82;
            BackupProgressText = "Creating backup archive on device...";
            await SshClientService.ExecuteCommandAsync(
                deviceConfig,
                $"tar -cf - -C '{remoteBaseDir}' '{backupName}' | gzip > '{remoteArchivePath}'");

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Save Firmware Backup",
                DefaultExtension = "tar.gz",
                InitialFileName = $"{backupName}.tar.gz",
                Filters = new List<FileDialogFilter>
                {
                    new() { Name = "Tar GZip Archive", Extensions = { "tar.gz" } },
                    new() { Name = "All Files", Extensions = { "*" } }
                }
            };

            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow == null)
            {
                BackupProgressText = "Unable to open save dialog.";
                return;
            }

            var savePath = await saveFileDialog.ShowAsync(mainWindow);
            if (string.IsNullOrWhiteSpace(savePath))
            {
                BackupProgressText = "Backup download canceled.";
                return;
            }

            BackupProgressValue = 92;
            BackupProgressText = "Downloading backup archive...";
            var archiveBytes = await SshClientService.DownloadFileBytesAsync(deviceConfig, remoteArchivePath);
            if (archiveBytes.Length == 0)
            {
                BackupProgressText = "Failed to download backup archive.";
                BackupProgressValue = 0;
                await _messageBoxService.ShowCustomMessageBox(
                    "Backup failed",
                    "The backup archive could not be downloaded. Please try again or open a ticket.",
                    ButtonEnum.Ok,
                    Icon.Error);
                return;
            }

            File.WriteAllBytes(savePath, archiveBytes);

            BackupProgressValue = 100;
            BackupProgressText = $"Backup saved to {savePath}";

            await SshClientService.ExecuteCommandAsync(
                deviceConfig,
                $"rm -rf '{remoteBackupDir}' '{remoteArchivePath}'");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error backing up firmware");
            BackupProgressText = $"Error: {ex.Message}";
            BackupProgressValue = 0;
            await _messageBoxService.ShowCustomMessageBox(
                "Backup failed",
                "An error occurred while creating the backup. Please try again or open a ticket.",
                ButtonEnum.Ok,
                Icon.Error);
        }
        finally
        {
            BackupInProgress = false;
            UpdateCanExecuteCommands();
        }
    }

    private static List<int> ParseMtdIndices(string mtdOutput)
    {
        return mtdOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => MtdLineRegex.Match(line))
            .Where(match => match.Success)
            .Select(match => match.Groups["dev"].Value.Replace("mtd", string.Empty, StringComparison.OrdinalIgnoreCase))
            .Select(index => int.TryParse(index, out var parsed) ? parsed : -1)
            .Where(index => index >= 0)
            .ToList();
    }

    private async Task RestoreFirmwareAsync()
    {
        var deviceConfig = DeviceConfig.Instance;
        if (!deviceConfig.CanConnect) return;

        var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;

        if (mainWindow == null)
        {
            RestoreProgressText = "Unable to open file dialog.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Select Firmware Backup",
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Firmware Backup", Extensions = { "gz", "tgz" } },
                new() { Name = "All Files", Extensions = { "*" } }
            }
        };

        var files = await dialog.ShowAsync(mainWindow);
        if (files == null || files.Length == 0 || string.IsNullOrWhiteSpace(files[0]))
        {
            RestoreProgressText = "Restore canceled.";
            return;
        }

        var archivePath = files[0];

        var confirm = await _messageBoxService.ShowCustomMessageBox(
            "Restore firmware?",
            $"This will overwrite all MTD flash partitions on the device with data from:\n{Path.GetFileName(archivePath)}\n\nThis is irreversible. A power failure mid-flash can permanently brick the device. Continue?",
            ButtonEnum.YesNo,
            Icon.Warning);

        if (confirm != ButtonResult.Yes) return;

        string tempDir = null;
        try
        {
            RestoreInProgress = true;
            RestoreProgressValue = 2;
            RestoreProgressText = "Extracting backup archive...";
            UpdateCanExecuteCommands();

            tempDir = ExtractBackupToTempDir(archivePath);

            RestoreProgressValue = 8;
            RestoreProgressText = "Verifying checksums...";

            var checksumFile = Directory.GetFiles(tempDir, "md5sums.txt", SearchOption.AllDirectories).FirstOrDefault()
                               ?? Directory.GetFiles(tempDir, "sha256sums.txt", SearchOption.AllDirectories).FirstOrDefault();
            var mtdBinFiles = Directory.GetFiles(tempDir, "mtdblock*.bin", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(tempDir, "mtd[0-9]*.bin", SearchOption.AllDirectories))
                .OrderBy(f =>
                {
                    var m = Regex.Match(Path.GetFileNameWithoutExtension(f), @"(\d+)$");
                    return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue;
                })
                .ToList();

            if (!mtdBinFiles.Any())
            {
                RestoreProgressText = "No MTD partition files found in the backup archive.";
                await _messageBoxService.ShowCustomMessageBox(
                    "Restore failed",
                    "No MTD partition files (mtd*.bin) found in the backup archive.",
                    ButtonEnum.Ok,
                    Icon.Error);
                return;
            }

            if (checksumFile != null)
                VerifyBackupChecksums(checksumFile, mtdBinFiles);

            RestoreProgressValue = 12;
            RestoreProgressText = "Reading device partition table...";

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var mtdResult = await SshClientService.ExecuteCommandWithResponseAsync(deviceConfig, "cat /proc/mtd", cts.Token);
            var deviceIndices = ParseMtdIndices(mtdResult?.Result ?? string.Empty);

            if (!deviceIndices.Any())
            {
                RestoreProgressText = "Could not read MTD partitions from device.";
                await _messageBoxService.ShowCustomMessageBox(
                    "Restore failed",
                    "Could not read /proc/mtd from device.",
                    ButtonEnum.Ok,
                    Icon.Error);
                return;
            }

            var progressPerPartition = Math.Max(1, 75 / mtdBinFiles.Count);

            for (var i = 0; i < mtdBinFiles.Count; i++)
            {
                var localPath = mtdBinFiles[i];
                var filename = Path.GetFileName(localPath);
                var indexMatch = Regex.Match(filename, @"mtdblock(\d+)\.bin|mtd(\d+)\.bin");
                if (!indexMatch.Success) continue;

                var partitionIndex = int.Parse(indexMatch.Groups[1].Success
                    ? indexMatch.Groups[1].Value
                    : indexMatch.Groups[2].Value);

                if (!deviceIndices.Contains(partitionIndex))
                {
                    Logger.Warning($"Skipping {filename}: /dev/mtd{partitionIndex} not found on device.");
                    continue;
                }

                var remotePath = $"{OpenIPC.RemoteTempFolder}/{filename}";

                RestoreProgressValue = 15 + (i * progressPerPartition);
                RestoreProgressText = $"Uploading {filename} ({i + 1}/{mtdBinFiles.Count})...";
                await SshClientService.UploadFileAsync(deviceConfig, localPath, remotePath);

                RestoreProgressValue = 15 + (i * progressPerPartition) + (progressPerPartition / 2);
                RestoreProgressText = $"Flashing {filename} to /dev/mtd{partitionIndex}...";
                await SshClientService.ExecuteCommandAsync(deviceConfig, $"flashcp -v '{remotePath}' /dev/mtd{partitionIndex}");

                await SshClientService.ExecuteCommandAsync(deviceConfig, $"rm -f '{remotePath}'");
            }

            RestoreProgressValue = 95;
            RestoreProgressText = "Rebooting device...";
            await SshClientService.ExecuteCommandAsync(deviceConfig, "reboot");

            RestoreProgressValue = 100;
            RestoreProgressText = "Restore complete. Device is rebooting.";

            await _messageBoxService.ShowCustomMessageBox(
                "Restore complete",
                "All MTD partitions have been restored. The device will reboot now.",
                ButtonEnum.Ok,
                Icon.Success);
        }
        catch (InvalidDataException ex)
        {
            Logger.Error(ex, "Checksum mismatch during restore");
            RestoreProgressText = $"Checksum mismatch: {ex.Message}";
            RestoreProgressValue = 0;
            await _messageBoxService.ShowCustomMessageBox(
                "Restore aborted",
                $"Checksum verification failed: {ex.Message}\n\nThe backup may be corrupted. No data was written to the device.",
                ButtonEnum.Ok,
                Icon.Error);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error restoring firmware");
            RestoreProgressText = $"Error: {ex.Message}";
            RestoreProgressValue = 0;
            await _messageBoxService.ShowCustomMessageBox(
                "Restore failed",
                "An error occurred during restore. Please try again or open a ticket.",
                ButtonEnum.Ok,
                Icon.Error);
        }
        finally
        {
            RestoreInProgress = false;
            RestoreConfirmed = false;
            if (tempDir != null && Directory.Exists(tempDir))
                try { Directory.Delete(tempDir, true); } catch { /* best effort cleanup */ }
            UpdateCanExecuteCommands();
        }
    }

    private string ExtractBackupToTempDir(string archivePath)
    {
        var tarPath = DecompressTgzToTar(archivePath);
        var tempDir = Path.Combine(Path.GetTempPath(), $"companion-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        using (var archive = SharpCompress.Archives.Tar.TarArchive.Open(tarPath))
        {
            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
            {
                var destPath = Path.Combine(tempDir, Path.GetFileName(entry.Key));
                using var entryStream = entry.OpenEntryStream();
                using var fileStream = File.Create(destPath);
                entryStream.CopyTo(fileStream);
            }
        }

        try { File.Delete(tarPath); } catch { /* best effort */ }
        return tempDir;
    }

    private static void VerifyBackupChecksums(string checksumFile, List<string> mtdBinFiles)
    {
        var checksumMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(checksumFile))
        {
            var spaceIdx = line.IndexOf(' ');
            if (spaceIdx < 0) continue;
            var hash = line[..spaceIdx].Trim();
            var name = Path.GetFileName(line[spaceIdx..].Trim());
            if (!string.IsNullOrEmpty(name))
                checksumMap[name] = hash;
        }

        using var md5 = System.Security.Cryptography.MD5.Create();
        foreach (var binPath in mtdBinFiles)
        {
            var name = Path.GetFileName(binPath);
            if (!checksumMap.TryGetValue(name, out var expectedHash)) continue;

            using var stream = File.OpenRead(binPath);
            var hashBytes = md5.ComputeHash(stream);
            var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{name}: expected {expectedHash[..8]}…, got {actualHash[..8]}…");
        }
    }

    private async Task ReplaceBootloaderAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedBootloader))
            return;

        if (!BootloaderConfirmed)
            return;

        var confirm = await _messageBoxService.ShowCustomMessageBox(
            "Replace bootloader?",
            $"This will flash '{SelectedBootloader}' to /dev/mtd0 and erase /dev/mtd1. Continue?",
            ButtonEnum.YesNo,
            Icon.Warning);

        if (confirm != ButtonResult.Yes)
            return;

        try
        {
            BootloaderInProgress = true;
            BootloaderProgressValue = 5;
            BootloaderProgressText = "Downloading bootloader...";
            UpdateCanExecuteCommands();

            var localPath = await DownloadBootloaderAsync(SelectedBootloader);
            if (string.IsNullOrEmpty(localPath))
            {
                BootloaderProgressText = "Download failed.";
                BootloaderProgressValue = 0;
                return;
            }

            var bootPartitionSize = await GetBootPartitionSizeAsync();
            if (bootPartitionSize.HasValue)
            {
                var localSize = new FileInfo(localPath).Length;
                if (localSize > bootPartitionSize.Value)
                {
                    BootloaderProgressText = "Bootloader too large for boot partition.";
                    BootloaderProgressValue = 0;
                    await _messageBoxService.ShowCustomMessageBox(
                        "Bootloader too large",
                        $"Selected bootloader is {localSize} bytes, but boot partition is {bootPartitionSize.Value} bytes. Choose the correct NOR/NAND bootloader.",
                        ButtonEnum.Ok,
                        Icon.Error);
                    return;
                }
            }

            BootloaderProgressValue = 30;
            BootloaderProgressText = "Uploading bootloader to device...";
            var remotePath = $"{OpenIPC.RemoteTempFolder}/{SelectedBootloader}";
            await SshClientService.UploadFileAsync(DeviceConfig.Instance, localPath, remotePath);

            BootloaderProgressValue = 60;
            BootloaderProgressText = "Flashing bootloader...";
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, $"flashcp -v {remotePath} /dev/mtd0");

            BootloaderProgressValue = 85;
            BootloaderProgressText = "Erasing environment partition...";
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, "flash_eraseall /dev/mtd1");

            BootloaderProgressValue = 95;
            BootloaderProgressText = "Rebooting device...";
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, "reboot");

            BootloaderProgressValue = 100;
            BootloaderProgressText = "Bootloader replaced.";
            await _messageBoxService.ShowCustomMessageBox(
                "Bootloader replaced",
                "Bootloader has been flashed. The device will reboot now.",
                ButtonEnum.Ok,
                Icon.Success);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error replacing bootloader");
            BootloaderProgressText = $"Error: {ex.Message}";
        }
        finally
        {
            BootloaderInProgress = false;
            UpdateCanExecuteCommands();
        }
    }

    private async Task PerformFirmwareUpgradeFromSocAsync()
    {
        try
        {
            ProgressValue = 0;
            
            Logger.Information("Performing firmware upgrade using selected dropdown options.");
            
            var firmwwareFile = SelectedFirmwareBySoc;
            
            var downloadUrl = string.Empty;
            var filename = string.Empty;
            
            if (!string.IsNullOrEmpty(firmwwareFile))
            {
                filename = firmwwareFile;
                downloadUrl = BuildFirmwareDownloadUrl(firmwwareFile);
            }
            
            else
            {
                Logger.Warning("Failed to construct firmware URL. Missing or invalid data.");
                return;
            }

            string firmwareFilePath = await DownloadFirmwareAsync(downloadUrl, filename);
            if (!string.IsNullOrEmpty(firmwareFilePath))
            {
                await UpgradeFirmwareFromFileAsync(firmwareFilePath);
            }
            
            
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing firmware upgrade from dropdown");
        }
    }
    private async Task PerformFirmwareUpgradeFromDropdownAsync()
    {
        try
        {
            ProgressValue = 0;

            if (string.IsNullOrEmpty(SelectedManufacturer) ||
                string.IsNullOrEmpty(SelectedDevice) ||
                string.IsNullOrEmpty(SelectedFirmware))
            {
                Logger.Warning("Cannot perform firmware upgrade. Missing dropdown selections.");
                return;
            }

            Logger.Information("Performing firmware upgrade using selected dropdown options.");

            var manufacturer = _firmwareData?.Manufacturers
                .FirstOrDefault(m => ((m.Name == SelectedManufacturer) || (m.FriendlyName == SelectedManufacturer)));

            var device = manufacturer?.Devices
                .FirstOrDefault(d => ((d.Name == SelectedDevice) || (d.FriendlyName == SelectedDevice)));

            var firmware = device?.FirmwarePackages
                .FirstOrDefault(f => ((f.Name == SelectedFirmware) || (f.FriendlyName == SelectedFirmware)));

            var downloadUrl = string.Empty;
            var filename = string.Empty;
            

            if (!string.IsNullOrEmpty(manufacturer?.Name) &&
                !string.IsNullOrEmpty(device?.Name) &&
                !string.IsNullOrEmpty(firmware?.Name))
            {
                filename = firmware.PackageFile;
                downloadUrl = BuildFirmwareDownloadUrl(filename);
            }
            
            else
            {
                Logger.Warning("Failed to construct firmware URL. Missing or invalid data.");
                return;
            }

            string firmwareFilePath = await DownloadFirmwareAsync(downloadUrl, filename);
            if (!string.IsNullOrEmpty(firmwareFilePath))
            {
                await UpgradeFirmwareFromFileAsync(firmwareFilePath);
            }
            
            
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error performing firmware upgrade from dropdown");
        }
    }


    public void CancelSysupgrade()
    {
        _cancellationTokenSource?.Cancel();
    }

    private void UpdateSysupgradeProgressFromLine(string rawLine)
    {
        if (!_sysupgradeInProgress)
            return;

        var line = NormalizeSysupgradeLine(rawLine);
        if (string.IsNullOrWhiteSpace(line))
            return;

        if (IsSysupgradeErrorLine(line))
        {
            SetProgressBarBrush(ProgressErrorBrush);
            return;
        }

        if (line.StartsWith("Uploading kernel", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.UploadKernel;
            SetProgressMinimum(UploadKernelStart);
            return;
        }

        if (line.StartsWith("Kernel binary uploaded successfully", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.UploadKernel;
            SetProgressMinimum(UploadKernelEnd);
            return;
        }

        if (line.StartsWith("Uploading root filesystem", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.UploadRootfs;
            SetProgressMinimum(UploadRootfsStart);
            return;
        }

        if (line.StartsWith("Root filesystem binary uploaded successfully", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.UploadRootfs;
            SetProgressMinimum(UploadRootfsEnd);
            return;
        }

        if (line.Equals("Kernel", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Update kernel from", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.KernelFlash;
            SetProgressMinimum(KernelFlashStart);
        }

        if (line.StartsWith("Kernel updated", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.KernelFlash;
            SetProgressMinimum(KernelFlashEnd);
        }

        if (line.Equals("RootFS", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Update rootfs from", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.RootfsFlash;
            SetProgressMinimum(RootfsFlashStart);
        }

        if (line.StartsWith("RootFS updated", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.RootfsFlash;
            SetProgressMinimum(RootfsFlashEnd);
        }

        if (line.StartsWith("OverlayFS", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Erase overlay partition", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.OverlayErase;
            SetProgressMinimum(OverlayEraseStart);
        }

        if (line.StartsWith("Waiting for device reboot", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.WaitingForOffline;
            SetProgressMinimum(WaitForOfflineStart);
            return;
        }

        if (line.StartsWith("Device went offline", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("Did not observe disconnect", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.WaitingForPing;
            SetProgressMinimum(WaitForPingStart);
            return;
        }

        if (line.StartsWith("Device is reachable again. Waiting for SSH", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.WaitingForSsh;
            SetProgressMinimum(WaitForSshStart);
            return;
        }

        if (line.StartsWith("Device reconnected successfully", StringComparison.OrdinalIgnoreCase))
        {
            _sysupgradePhase = SysupgradePhase.WaitingForSsh;
            SetProgressMinimum(99);
            return;
        }

        if (TryParseSysupgradePercent(line, out var percent))
            ApplySysupgradePercent(percent);
    }

    private static string NormalizeSysupgradeLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return string.Empty;

        var normalized = AnsiEscapeRegex.Replace(line, string.Empty);
        normalized = AnsiBracketRegex.Replace(normalized, string.Empty);
        return normalized.Trim();
    }

    private static bool TryParseSysupgradePercent(string line, out int percent)
    {
        percent = 0;
        var match = FlashPercentRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups["percent"].Value, out percent))
            return true;

        match = OverlayPercentRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups["percent"].Value, out percent))
            return true;

        match = RecoveryPercentRegex.Match(line);
        if (match.Success && int.TryParse(match.Groups["percent"].Value, out percent))
            return true;

        return false;
    }

    private static bool IsSysupgradeErrorLine(string line)
    {
        return line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Command did not complete successfully", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Command execution timed out", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Unable to connect to the host", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Connection lost", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplySysupgradePercent(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        switch (_sysupgradePhase)
        {
            case SysupgradePhase.KernelFlash:
                SetProgressMinimum(MapPercentToRange(KernelFlashStart, KernelFlashEnd, percent));
                break;
            case SysupgradePhase.RootfsFlash:
                SetProgressMinimum(MapPercentToRange(RootfsFlashStart, RootfsFlashEnd, percent));
                break;
            case SysupgradePhase.OverlayErase:
                SetProgressMinimum(MapPercentToRange(OverlayEraseStart, OverlayEraseEnd, percent));
                break;
            case SysupgradePhase.WaitingForOffline:
                SetProgressMinimum(MapPercentToRange(WaitForOfflineStart, WaitForOfflineEnd, percent));
                break;
            case SysupgradePhase.WaitingForPing:
                SetProgressMinimum(MapPercentToRange(WaitForPingStart, WaitForPingEnd, percent));
                break;
            case SysupgradePhase.WaitingForSsh:
                SetProgressMinimum(MapPercentToRange(WaitForSshStart, WaitForSshEnd, percent));
                break;
        }
    }

    private static int MapPercentToRange(int rangeStart, int rangeEnd, int percent)
    {
        return rangeStart + (int)Math.Round((rangeEnd - rangeStart) * (percent / 100.0));
    }

    private void SetProgressMinimum(int value)
    {
        if (value <= ProgressValue)
            return;

        ProgressValue = value;
        Logger.Debug("Sysupgrade ProgressValue: " + ProgressValue);
    }

    private void SetProgressBarBrush(IBrush brush)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetProgressBarBrush(brush));
            return;
        }

        ProgressBarBrush = brush;
    }

    private async Task UpgradeFirmwareFromFileAsync(string firmwareFilePath)
    {
        try
        {
            Logger.Information($"Upgrading firmware from file: {firmwareFilePath}");
            SetProgressBarBrush(ProgressRunningBrush);

            ProgressValue = 5;
            Logger.Debug("UncompressFirmware ProgressValue: " + ProgressValue);
            var extractedDir = UncompressFirmware(firmwareFilePath);

            ProgressValue = 10;
            Logger.Debug("ValidateFirmwareFiles ProgressValue: " + ProgressValue);
            ValidateFirmwareFiles(extractedDir);

            ProgressValue = 20;
            Logger.Debug("Before PerformSysupgradeAsync ProgressValue: " + ProgressValue);
            var kernelFile = Directory.GetFiles(extractedDir)
                .FirstOrDefault(f => f.Contains("uImage") && !f.EndsWith(".md5sum"));

            var rootfsFile = Directory.GetFiles(extractedDir)
                .FirstOrDefault(f => f.Contains("rootfs") && !f.EndsWith(".md5sum"));

            if (kernelFile == null || rootfsFile == null)
                throw new InvalidOperationException("Kernel or RootFS file is missing after validation.");

            var sysupgradeService = new SysUpgradeService(SshClientService, Logger);

            _sysupgradeInProgress = true;
            _sysupgradePhase = SysupgradePhase.None;
            await Task.Run(async () =>
            {
                await sysupgradeService.PerformSysupgradeAsync(DeviceConfig.Instance, kernelFile, rootfsFile,
                    progress =>
                    {
                        Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            UpdateSysupgradeProgressFromLine(progress);

                            Logger.Debug(progress);
                        });
                    },
                    CancellationToken.None);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    //ProgressValue = 100;
                    Logger.Information("Firmware upgrade completed successfully.");
                });
            });

            _sysupgradeInProgress = false;
            _sysupgradePhase = SysupgradePhase.None;
            ProgressValue = 100;
            SetProgressBarBrush(ProgressCompleteBrush);
            var deviceUrl = $"http://{DeviceConfig.Instance.IpAddress}";
            var result = await _messageBoxService.ShowCustomMessageBox(
                "Upgrade Complete!!",
                $"Device has been flashed!\n\nOpen device page:\n{deviceUrl}\n\nOpen this link now?",
                ButtonEnum.YesNo,
                Icon.Success);

            if (result == ButtonResult.Yes)
                OpenUrl(deviceUrl);

            Logger.Information("Firmware upgrade completed successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error upgrading firmware from file");
            SetProgressBarBrush(ProgressErrorBrush);
            _sysupgradeInProgress = false;
            _sysupgradePhase = SysupgradePhase.None;
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public async Task SelectLocalFirmwarePackage(Window window)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select a File",
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Compressed", Extensions = { "tgz" } },
                new() { Name = "Bin Files", Extensions = { "bin" } },
                new() { Name = "All Files", Extensions = { "*" } }
            }
        };

        var result = await dialog.ShowAsync(window);

        if (result != null && result.Length > 0)
        {
            var selectedFile = result[0];
            var fileName = Path.GetFileName(selectedFile);
            Console.WriteLine($"Selected File: {selectedFile}");
            _bRecursionSelectGuard = true;
            ManualLocalFirmwarePackageFile = selectedFile;
            IsLocalFirmwarePackageSelected = true;
            IsManufacturerDeviceFirmwareComboSelected = false;
            SelectedManufacturer = string.Empty;
            SelectedDevice = string.Empty;
            SelectedFirmware = string.Empty;
            SelectedFirmwareBySoc = string.Empty;
            IsFirmwareBySocSelected = false;
            _bRecursionSelectGuard = false;

            UpdateCanExecuteCommands();
        }
    }

    private bool IsGregApfpvSourceSelected()
    {
        return string.Equals(SelectedFirmwareSource, GregApfpvFirmwareSource, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildFirmwareDownloadUrl(string firmwareFilename)
    {
        if (IsGregApfpvSourceSelected())
            return $"{OpenIPC.GregApfpvRawBaseUrl}{firmwareFilename}";

        return $"https://github.com/OpenIPC/builder/releases/download/latest/{firmwareFilename}";
    }

    #endregion
}

#region Support Classes

// Firmwares are organised as follow:
// Manufacturers -> Devices -> Specific Firmware Package
// Ex:
//    Manufacturer A:
//        Device 1:
//             Firmware package a (ie. OpenIPC-FPV for SSC338Q);
//             Firmware package b (ie. RubyFPV);
//        Device 2:
//             Firmware package a;
//             Firmware package b;
//             Firmware package c;
//             Firmware package d;
//        ...
//    Manufacturer B:
//        Device 1:
//             Firmware package a;
//        ...
// To allow for:
//    * generic firmwares that can either be used on multiple devices from same manufacturer,
//      either can be used for multiple manufacturers,
//    * generic manufacturers (ie. Aliexpress SSC338Q generic boards)
//
// a wildcard Manufacturer and a wildcard Device is present in the tree at each mode, where appropiate, denoted with id "*" and friendly name "Generic"
//
public class FirmwareData
{
    public ObservableCollection<Manufacturer> Manufacturers { get; set; }

    public void SortCollections()
    {
        Manufacturers = new ObservableCollection<Manufacturer>(Manufacturers.OrderBy(p => p.FriendlyName));
        var genericMan = Manufacturers.Single(p => p.Name.Equals("*"));
        Manufacturers.Remove(Manufacturers.Where(p => p.Name.Equals("*")).Single());
        Manufacturers.Insert(0, genericMan);
        foreach (var manufacturer in Manufacturers)
        {
            manufacturer.SortCollections();
        }
    }
}

public class Manufacturer
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public ObservableCollection<Device> Devices { get; set; }

    public void SortCollections()
    {
        Devices = new ObservableCollection<Device>(Devices.OrderBy(p => p.FriendlyName));
        foreach (var device in Devices)
        {
            device.SortCollections();
        }
    }
}

public class Device
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public ObservableCollection<FirmwarePackage> FirmwarePackages { get; set; }

    public void SortCollections()
    {
        FirmwarePackages = new ObservableCollection<FirmwarePackage>(FirmwarePackages.OrderBy(p => p.FriendlyName));
    }
}

public class FirmwarePackage
{
    public string Name { get; set; }
    public string FriendlyName { get; set; }
    public string PackageFile { get; set; }
}
#endregion
