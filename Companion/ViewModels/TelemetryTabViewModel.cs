using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing telemetry settings and configuration
/// </summary>
public partial class TelemetryTabViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IMessageBoxService _messageBoxService;
    private readonly IYamlConfigService _yamlConfigService;
    private readonly Dictionary<string, string> _yamlConfig = new();
    private readonly IGlobalSettingsService _globalSettingsService;
    
    #endregion

    #region Public Properties
    public bool IsMobile => App.OSType == "Mobile";
    public bool IsEnabledForView => CanConnect && !IsMobile;
    
    public bool IsWfbYamlEnabled => _globalSettingsService.IsWfbYamlEnabled;
    public bool CanEnable40MhzAction => CanConnect && !IsWfbYamlEnabled;
    
    // Computed property for selective disabling

    #endregion

    #region Observable Properties
    [ObservableProperty] private bool _canConnect;
    
    [ObservableProperty] private string _selectedAggregate;
    [ObservableProperty] private string _selectedBaudRate;
    [ObservableProperty] private string _selectedMcsIndex;
    [ObservableProperty] private string _selectedRcChannel;
    [ObservableProperty] private string _selectedRouter;
    [ObservableProperty] private string _selectedMSPFps;
    [ObservableProperty] private string _selectedSerialPort;
    [ObservableProperty] private string _telemetryContent;
    
    

    
    
    // Add these observable properties to your #region Observable Properties section
    [ObservableProperty] private bool _isSerialPortEnabled = true;
    [ObservableProperty] private bool _isBaudRateEnabled = true;
    [ObservableProperty] private bool _isRouterEnabled = true;
    [ObservableProperty] private bool _isMSPFpsEnabled = false;
    [ObservableProperty] private bool _isMcsIndexEnabled = true;
    [ObservableProperty] private bool _isAggregateEnabled = true;
    [ObservableProperty] private bool _isRcChannelEnabled = true;
    #endregion

    #region Constants
// Add this dictionary to map between numeric values and descriptive names
    private readonly Dictionary<string, string> _routerMapping = new Dictionary<string, string>
    {
        { "0", "mavfwd" },
        { "1", "mavlink-routed" },
        { "2", "msposd" }
    };

// Reverse mapping for saving
    private readonly Dictionary<string, string> _reverseRouterMapping = new Dictionary<string, string>
    {
        { "mavfwd", "0" },
        { "mavlink-routed", "1" },
        { "msposd", "2" }
    };
    #endregion
    
    #region Collections
    /// <summary>
    /// Available serial ports for telemetry
    /// </summary>
    public ObservableCollection<string> SerialPorts { get; private set; }

    /// <summary>
    /// Available baud rates for serial communication
    /// </summary>
    public ObservableCollection<string> BaudRates { get; private set; }

    /// <summary>
    /// Available MCS index values
    /// </summary>
    public ObservableCollection<string> McsIndex { get; private set; }

    /// <summary>
    /// Available aggregate values
    /// </summary>
    public ObservableCollection<string> Aggregate { get; private set; }

    /// <summary>
    /// Available RC channel options
    /// </summary>
    public ObservableCollection<string> RC_Channel { get; private set; }

    /// <summary>
    /// Available router options
    /// </summary>
    public ObservableCollection<string> Router { get; private set; }
    /// <summary>
    /// Available msposd fps options
    /// </summary>
    public ObservableCollection<string> MSPFps { get; private set; }

    #endregion

    #region Commands
    public ICommand EnableUART0Command { get; private set; }
    public ICommand DisableUART0Command { get; private set; }
    public ICommand AddMavlinkCommand { get; private set; }
    public ICommand UploadLatestVtxMenuCommand { get; private set; }
    public ICommand Enable40MhzCommand { get; private set; }
    public ICommand MSPOSDExtraCameraCommand { get; private set; }
    public ICommand MSPOSDExtraGSCommand { get; private set; }
    public ICommand RemoveMSPOSDExtraCommand { get; private set; }
    public ICommand SaveAndRestartTelemetryCommand { get; private set; }
    
    
    
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of TelemetryTabViewModel
    /// </summary>
    public TelemetryTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IMessageBoxService messageBoxService,
        IYamlConfigService yamlConfigService,
        IGlobalSettingsService globalSettingsService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _messageBoxService = messageBoxService;
        _yamlConfigService = yamlConfigService;
        _globalSettingsService = globalSettingsService;

        InitializeCollections();
        InitializeCommands();
        SubscribeToEvents();
    }
    #endregion

    #region Initialization Methods
    private void InitializeCollections()
    {
        SerialPorts = new ObservableCollection<string> { "ttyS0", "ttyS1", "ttyS2", "ttyS3" };
        BaudRates = new ObservableCollection<string> { "4800", "9600", "19200", "38400", "57600", "115200" };
        McsIndex = new ObservableCollection<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        Aggregate = new ObservableCollection<string> { "0", "1", "2", "4", "6", "8", "10", "12", "14", "15" };
        RC_Channel = new ObservableCollection<string> { "0", "1", "2", "3", "4", "5", "6", "7", "8" };
        Router = new ObservableCollection<string> { "mavfwd", "mavlink-routed", "msposd" }; // 0,1,2 telemetry.conf
        MSPFps = new ObservableCollection<string> { "20", "30","60", "90", "100", "120" }; 
    }

    private void InitializeCommands()
    {
        EnableUART0Command = new AsyncRelayCommand(EnableUART0);
        DisableUART0Command = new AsyncRelayCommand(DisableUART0);
        AddMavlinkCommand = new AsyncRelayCommand(AddMavlink);
        UploadLatestVtxMenuCommand = new AsyncRelayCommand(UploadLatestVtxMenu);
        Enable40MhzCommand = new AsyncRelayCommand(Enable40Mhz);
        MSPOSDExtraCameraCommand = new AsyncRelayCommand(AddMSPOSDCameraExtra);
        MSPOSDExtraGSCommand = new AsyncRelayCommand(AddMSPOSDGSExtra);
        RemoveMSPOSDExtraCommand = new AsyncRelayCommand(RemoveMSPOSDExtra);
        SaveAndRestartTelemetryCommand = new AsyncRelayCommand(SaveAndRestartTelemetry);
        // Initialize the ToggleAlinkDroneCommand properly
        
    }

    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<TelemetryContentUpdatedEvent, TelemetryContentUpdatedMessage>(
            OnTelemetryContentUpdated);
        
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
        
        EventSubscriptionService.Subscribe<WfbYamlContentUpdatedEvent, WfbYamlContentUpdatedMessage>(
            OnWfbYamlContentUpdated);
        
        
    }
    #endregion

    #region Event Handlers
    private void OnAppMessage(AppMessage appMessage)
    {
        CanConnect = appMessage.CanConnect;
    }

    public virtual void HandleTelemetryContentUpdated(TelemetryContentUpdatedMessage message)
    {
        TelemetryContent = message.Content;
        ParseTelemetryContent();
    }

    private void OnTelemetryContentUpdated(TelemetryContentUpdatedMessage message)
    {
        HandleTelemetryContentUpdated(message);
    }
    #endregion

    #region Control Handlers
    partial void OnSelectedSerialPortChanged(string value)
    {
        Logger.Debug($"SelectedSerialPortChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.TelemetrySerialPort, value.ToString());   
    }
    
    partial void OnSelectedRouterChanged(string value)
    {
        Logger.Debug($"SelectedRouterChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.TelemetryRouter, value.ToString());   
    }
    partial void OnSelectedMSPFpsChanged(string value)
    {
        Logger.Debug($"SelectedMSPFpsChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.TelemetryOsdFps, value.ToString());   
    }
    #endregion
    
    #region Command Handlers
    private async Task EnableUART0()
    {
        UpdateUIMessage("Enabling UART0...");
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.UART0OnCommand);
    }

    private async Task DisableUART0()
    {
        UpdateUIMessage("Disabling UART0...");
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.UART0OffCommand);
    }

    private async Task AddMavlink()
    {
        UpdateUIMessage("Adding MAVLink...");
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, TelemetryCommands.Extra);
        await RebootDevice();
    }

    private async Task UploadLatestVtxMenu()
    {
        Log.Debug("UploadLatestVtxMenu executed");

        // upload vtxmenu.ini /etc
        await SshClientService.UploadBinaryAsync(DeviceConfig.Instance, OpenIPC.RemoteEtcFolder, "vtxmenu.ini");

        // ensure file is unix formatted
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, "dos2unix /etc/vtxmenu.ini");

        // reboot
        await RebootDevice();
    }

    private async Task Enable40Mhz()
    {
        // if using wfb.yaml system don't do this
        UpdateUIMessage("Enabling 40MHz...");
        await SshClientService.UploadFileAsync(DeviceConfig.Instance, OpenIPC.LocalWifiBroadcastBinFileLoc,
            OpenIPC.RemoteWifiBroadcastBinFileLoc);
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance,
            $"{DeviceCommands.Dos2UnixCommand} {OpenIPC.RemoteWifiBroadcastBinFileLoc}");
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance,
            $"chmod +x {OpenIPC.RemoteWifiBroadcastBinFileLoc}");
        UpdateUIMessage("Enabling 40MHz...done");
    }

    private async Task RemoveMSPOSDExtra()
    {
        Log.Debug("Remove MSPOSDExtra executed");

        if (IsWfbYamlEnabled)
        {
            
        }
        else
        {

            var remoteTelemetryFile = Path.Join(OpenIPC.RemoteBinariesFolder, "telemetry");

            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance,
                $"sed -i 's/sleep 5/#sleep 5/' {remoteTelemetryFile}");
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.DataLinkRestart);

            _messageBoxService.ShowMessageBox("Done!", "Please wait for datalink to restart!");
        }
    }

    private async Task AddMSPOSDCameraExtra()
    {
        Log.Debug("MSPOSDExtra executed");
        if (IsWfbYamlEnabled)
        {
            Log.Debug("Wfb yaml");
            
        }
        else
        {
            Log.Debug("Legacy Wfb");
            var telemetryFile = Path.Join(OpenIPC.GetBinariesPath(), "clean", "telemetry_msposd_extra");
            var remoteTelemetryFile = Path.Join(OpenIPC.RemoteBinariesFolder, "telemetry");

            await SshClientService.UploadFileAsync(DeviceConfig.Instance, telemetryFile, remoteTelemetryFile);
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, "chmod +x " + remoteTelemetryFile);
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.DataLinkRestart);


            _messageBoxService.ShowMessageBox("Done!", "Please wait for datalink to restart!");
        }
        
    }

    private async Task AddMSPOSDGSExtra()
    {
        Log.Debug("MSPOSDExtra executed");

        if (IsWfbYamlEnabled)
        {
            
        }
        else
        {
            Log.Debug("Legacy Wfb");
            var telemetryFile = Path.Join(OpenIPC.GetBinariesPath(), "clean", "telemetry_msposd_gs");
            var remoteTelemetryFile = Path.Join(OpenIPC.RemoteBinariesFolder, "telemetry");

            await SshClientService.UploadFileAsync(DeviceConfig.Instance, telemetryFile, remoteTelemetryFile);
            await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.DataLinkRestart);

            _messageBoxService.ShowMessageBox("Done!", "Please wait for datalink to restart!");
        }
    }

    private async Task SaveAndRestartTelemetry()
    {
        if (_globalSettingsService.IsWfbYamlEnabled)
        {
            Log.Debug("Saving WFB YAML...");
            try
            {
                var updatedYamlContent = _yamlConfigService.UpdateYaml(_yamlConfig);
            
                await SshClientService.UploadFileStringAsync(
                    DeviceConfig.Instance,
                    OpenIPC.WfbYamlFileLoc,
                    updatedYamlContent);

                await RebootDevice();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update Wfb.yaml configuration: {ExceptionMessage}", ex.Message);
                return;
            }
            

            //Logger.Information("wfb.yaml configuration updated and service is restarting.");
        }
        else
        {
            Log.Debug("Saving and restarting telemetry...");
            TelemetryContent = UpdateTelemetryContent(SelectedSerialPort, SelectedBaudRate, SelectedRouter,
                SelectedMcsIndex, SelectedAggregate, SelectedRcChannel);
            await SshClientService.UploadFileStringAsync(DeviceConfig.Instance, OpenIPC.TelemetryConfFileLoc,
                TelemetryContent);
            
            await RebootDevice();
        }
        
    }

    private async Task RebootDevice()
    {
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.RebootCommand);

        _messageBoxService.ShowMessageBox("Rebooting Device!", "Rebooting device, please wait for device to be ready and reconnect, then validate settings.");
    }

    #endregion

    #region Helper Methods
    
    private void UpdateControlStates()
    {
        // Default state - all enabled if connected
        var defaultState = CanConnect;
    
        IsSerialPortEnabled = defaultState;
    
        // For BaudRate, you already have a specific flag
        IsBaudRateEnabled = defaultState && (_globalSettingsService.IsWfbYamlEnabled ? false : true);
    
        IsRouterEnabled = defaultState;
        IsMSPFpsEnabled = defaultState;
        IsMcsIndexEnabled = defaultState && !_globalSettingsService.IsWfbYamlEnabled;
        IsAggregateEnabled = defaultState && !_globalSettingsService.IsWfbYamlEnabled;
        IsRcChannelEnabled = defaultState && !_globalSettingsService.IsWfbYamlEnabled;
    }
    
    private void OnWfbYamlContentUpdated(WfbYamlContentUpdatedMessage message)
    { 
        _yamlConfigService.ParseYaml(message.Content, _yamlConfig);
        ParseWfbYamlContent();
    }
    
    /// <summary>
    /// Parses telemetry content and updates corresponding properties
    /// </summary>
    private void ParseTelemetryContent()
    {
        Logger.Debug("Parsing TelemetryContent.");
        if (string.IsNullOrEmpty(TelemetryContent)) return;

        var lines = TelemetryContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // Example: Parse key-value pairs
            var parts = line.Split('=');
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                UpdatePropertyFromTelemetryLine(key, value);
            }
        }
    }

    private void ParseWfbYamlContent()
    {
        Debug.WriteLine("ParseWfbYamlContent");
        UpdateControlStates();

        IsMSPFpsEnabled = true;

        UpdateViewModelPropertiesFromYaml();
    }


    private void UpdateViewModelPropertiesFromYaml()
    {
        if (_yamlConfig.TryGetValue(WfbYaml.TelemetrySerialPort, out var serialPort))
        {
            if (SerialPorts?.Contains(serialPort) ?? false)
            {
                SelectedSerialPort = serialPort;
            }
            else
            {
                SerialPorts.Add(serialPort);
                SelectedSerialPort = serialPort;
            }
        }
        
        if (_yamlConfig.TryGetValue(WfbYaml.TelemetryRouter, out var router))
        {
            if (Router?.Contains(router) ?? false)
            {
                SelectedRouter = router;
            }
            else
            {
                Router.Add(router);
                SelectedRouter = router;
            }
            
        }
        
        if (_yamlConfig.TryGetValue(WfbYaml.TelemetryOsdFps, out var osd_fps))
        {
            if (MSPFps?.Contains(osd_fps) ?? false)
            {
                SelectedMSPFps = osd_fps;
            }
            else
            {
                MSPFps.Add(osd_fps);
                SelectedMSPFps = osd_fps;
            }
            
        }

        
    }
    /// <summary>
    /// Updates the corresponding property based on the telemetry line key-value pair
    /// </summary>
    private void UpdatePropertyFromTelemetryLine(string key, string value)
    {
        switch (key)
        {
            case Telemetry.Serial:
                // Extract just the base name from the full path (e.g., "ttyS0" from "/dev/ttyS0")
                string serialPortBaseName = value;
                if (value.StartsWith("/dev/"))
                {
                    serialPortBaseName = value.Substring("/dev/".Length);
                }

                if (SerialPorts?.Contains(serialPortBaseName) ?? false)
                {
                    SelectedSerialPort = serialPortBaseName;
                }
                else
                {
                    SerialPorts.Add(serialPortBaseName);
                    SelectedSerialPort = serialPortBaseName;
                }
                break;

            case Telemetry.Baud:
                if (BaudRates?.Contains(value) ?? false)
                {
                    SelectedBaudRate = value;
                }
                else
                {
                    BaudRates.Add(value);
                    SelectedBaudRate = value;
                }
                break;

            case Telemetry.Router:
                // Convert numeric value to descriptive name if possible
                string routerName = value;
                if (_routerMapping.TryGetValue(value, out var mappedRouter))
                {
                    routerName = mappedRouter;
                }
            
                if (Router?.Contains(routerName) ?? false)
                {
                    SelectedRouter = routerName;
                }
                else
                {
                    Router.Add(routerName);
                    SelectedRouter = routerName;
                }
                break;

            case Telemetry.McsIndex:
                if (McsIndex?.Contains(value) ?? false)
                {
                    SelectedMcsIndex = value;
                }
                else
                {
                    McsIndex.Add(value);
                    SelectedMcsIndex = value;
                }
                break;

            case Telemetry.Aggregate:
                if (Aggregate?.Contains(value) ?? false)
                {
                    SelectedAggregate = value;
                }
                else
                {
                    Aggregate.Add(value);
                    SelectedAggregate = value;
                }
                break;

            case Telemetry.RcChannel:
                if (RC_Channel?.Contains(value) ?? false)
                {
                    SelectedRcChannel = value;
                }
                else
                {
                    RC_Channel.Add(value);
                    SelectedRcChannel = value;
                }
                break;

            default:
                Logger.Debug($"Telemetry - Unknown key: {key}, value: {value}");
                break;
        }
    }


    /// <summary>
    /// Updates telemetry content with new configuration values
    /// </summary>
    private string UpdateTelemetryContent(
        string serial,
        string baudRate,
        string router,
        string mcsIndex,
        string aggregate,
        string rcChannel)
    {
        // Convert the short serial port name back to the full path if needed
        string fullSerialPath = serial.StartsWith("/dev/") ? serial : $"/dev/{serial}";
        
        // Convert descriptive router name to numeric value if needed
        string routerValue = router;
        if (_reverseRouterMapping.TryGetValue(router, out var mappedRouterValue))
        {
            routerValue = mappedRouterValue;
        }
        
        var regex = new Regex(@"(serial|baud|router|mcs_index|aggregate|channels)=.*");
        return regex.Replace(TelemetryContent, match =>
        {
            return match.Groups[1].Value switch
            {
                Telemetry.Serial => $"serial={fullSerialPath}",
                Telemetry.Baud => $"baud={baudRate}",
                Telemetry.Router => $"router={routerValue}",
                Telemetry.McsIndex => $"mcs_index={mcsIndex}",
                Telemetry.Aggregate => $"aggregate={aggregate}",
                Telemetry.RcChannel => $"channels={rcChannel}",
                _ => match.Value
            };
        });
    }
    
    public void UpdateYamlConfig(string key, string newValue)
    {
        if (_yamlConfig.ContainsKey(key))
            _yamlConfig[key] = newValue;
        else
            _yamlConfig.Add(key, newValue);

        if (string.IsNullOrEmpty(newValue))
            _yamlConfig.Remove(key);
    }
    
    

    
    
    #endregion
}
