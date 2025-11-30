using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData.Binding;
using MsBox.Avalonia;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

/// <summary>
/// ViewModel for managing WiFi Broadcast (WFB) settings and configuration
/// </summary>
public partial class WfbTabViewModel : ViewModelBase
{
    #region Private Fields
    private readonly Dictionary<int, string> _24FrequencyMapping = FrequencyMappings.Frequency24GHz;
    private readonly Dictionary<int, string> _58FrequencyMapping = FrequencyMappings.Frequency58GHz;
    private bool _isDisposed;
    private readonly IYamlConfigService _yamlConfigService;
    private readonly Dictionary<string, string> _yamlConfig = new();
    private readonly IGlobalSettingsService _globalSettingsService;
    #endregion

    #region Observable Properties
    [ObservableProperty] private bool _canConnect;
    [ObservableProperty] private string _wfbConfContent;
    [ObservableProperty] private string _wfbYamlContent;
    [ObservableProperty] private int _selectedChannel;
    [ObservableProperty] private int _selectedPower24GHz;
    [ObservableProperty] private int _selectedBandwidth;
    [ObservableProperty] private int _selectedPower;
    [ObservableProperty] private int _selectedLdpc;
    [ObservableProperty] private int _selectedMcsIndex;
    [ObservableProperty] private int _selectedStbc;
    [ObservableProperty] private int _selectedFecK;
    [ObservableProperty] private int _selectedFecN;
    [ObservableProperty] private string _selectedFrequency24String;
    [ObservableProperty] private string _selectedFrequency58String;
    #endregion

    #region Collections
    [ObservableProperty] private ObservableCollection<string> _frequencies58GHz;
    [ObservableProperty] private ObservableCollection<string> _frequencies24GHz;
    [ObservableProperty] private ObservableCollection<int> _power58GHz;
    [ObservableProperty] private ObservableCollection<int> _power24GHz;
    [ObservableProperty] private ObservableCollection<int> _bandwidth;
    [ObservableProperty] private ObservableCollection<int> _mcsIndex;
    [ObservableProperty] private ObservableCollection<int> _stbc;
    [ObservableProperty] private ObservableCollection<int> _ldpc;
    [ObservableProperty] private ObservableCollection<int> _fecK;
    [ObservableProperty] private ObservableCollection<int> _fecN;
    [ObservableProperty] private int _mlink = 0;
    [ObservableProperty] private int _maxPower58GHz = 60;
    [ObservableProperty] private int _maxPower24GHz = 60;
    #endregion

    #region Commands
    /// <summary>
    /// Command to restart the WFB service
    /// </summary>
    public ICommand RestartWfbCommand { get; set; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of WfbTabViewModel
    /// </summary>
    public WfbTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IYamlConfigService yamlConfigService,
        IGlobalSettingsService globalSettingsSettingsViewModel)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _yamlConfigService = yamlConfigService ?? throw new ArgumentNullException(nameof(yamlConfigService));
        _globalSettingsService = globalSettingsSettingsViewModel ?? throw new ArgumentNullException(nameof(globalSettingsSettingsViewModel));
        
        // read device to determine configurations
        _globalSettingsService.ReadDevice();
        Logger.Debug($"IsWfbYamlEnabled = {_globalSettingsService.IsWfbYamlEnabled}");

        
        InitializeCollections();
        InitializeCommands();
        SubscribeToEvents();
        
    }
    #endregion

    #region Initialization Methods
    private void InitializeCollections()
    {
        Frequencies58GHz = new ObservableCollectionExtended<string>(_58FrequencyMapping.Values);
        Frequencies24GHz = new ObservableCollectionExtended<string>(_24FrequencyMapping.Values);

        Power58GHz = new ObservableCollection<int>(Enumerable.Range(1, MaxPower58GHz).Select(i => (i * 1)));
        Power24GHz = new ObservableCollection<int>(Enumerable.Range(1, MaxPower24GHz).Select(i => (i * 1)));

        Bandwidth = new ObservableCollectionExtended<int> { 20, 40 };
        McsIndex = new ObservableCollectionExtended<int>(Enumerable.Range(0, 31));
        Stbc = new ObservableCollectionExtended<int> { 0, 1 };
        Ldpc = new ObservableCollectionExtended<int> { 0, 1 };
        FecK = new ObservableCollectionExtended<int>(Enumerable.Range(0, 20));
        FecN = new ObservableCollectionExtended<int>(Enumerable.Range(0, 20));
    }

    private void InitializeCommands()
    {
        RestartWfbCommand = new RelayCommand(RestartWfb);
    }


    partial void OnSelectedChannelChanged(int value)
    {
        Logger.Verbose($"SelectedChannelChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.WfbChannel, value.ToString());
    }
    partial void OnSelectedPowerChanged(int value)
    {
        Logger.Verbose($"SelectedPowerChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.WfbTxPower, value.ToString());
    }
    partial void OnSelectedMcsIndexChanged(int value)
    {
        Logger.Verbose($"SelectedMcsIndexStringChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.BroadcastMcsIndex, value.ToString());
    }
    partial void OnSelectedFecKChanged(int value)
    {
        Logger.Verbose($"SelectedFecKChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.BroadcastFecK, value.ToString());
    }
    partial void OnSelectedFecNChanged(int value)
    {
        Logger.Verbose($"SelectedFecNChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.BroadcastFecN, value.ToString());
    }
    partial void OnSelectedLdpcChanged(int value)
    {
        Logger.Verbose($"SelectedLdpcChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.BroadcastLdpc, value.ToString());
    }
    partial void OnSelectedStbcChanged(int value)
    {
        Logger.Verbose($"SelectedStbcChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.BroadcastStbc, value.ToString());
    }
    
    partial void OnSelectedBandwidthChanged(int value)
    {
        Logger.Verbose($"SelectedBandwidthChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.WfbBandwidth, value.ToString());
    }

    partial void OnMlinkChanging(int value)
    {
        Logger.Verbose($"SelectedMlinkChanged updated to {value}");
        UpdateYamlConfig(WfbYaml.WfbMlink, value.ToString());
    }


    
    private void SubscribeToEvents()
    {
        EventSubscriptionService.Subscribe<WfbYamlContentUpdatedEvent, WfbYamlContentUpdatedMessage>(
            OnWfbYamlContentUpdated);
        
        EventSubscriptionService.Subscribe<WfbConfContentUpdatedEvent, WfbConfContentUpdatedMessage>(
            OnWfbConfContentUpdated);
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
    }
    #endregion

    #region Event Handlers
    private void OnWfbConfContentUpdated(WfbConfContentUpdatedMessage message)
    {
        WfbConfContent = message.Content;
        ParseWfbConfContent();
    }
    
    private void OnWfbYamlContentUpdated(WfbYamlContentUpdatedMessage message)
    {
        WfbYamlContent = message.Content;
        _yamlConfigService.ParseYaml(message.Content, _yamlConfig);
        ParseWfbYamlContent();
        
    }

    private void OnAppMessage(AppMessage message)
    {
        CanConnect = message.CanConnect;
    }
    #endregion

    #region Property Change Handlers
    partial void OnWfbConfContentChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) ParseWfbConfContent();
    }

    partial void OnSelectedFrequency24StringChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) HandleFrequencyChange(value, _24FrequencyMapping);
    }

    partial void OnSelectedFrequency58StringChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) HandleFrequencyChange(value, _58FrequencyMapping);
    }
    #endregion

    #region Command Handlers

    private async Task updateWfbConfContent()
    {
        UpdateUIMessage("Restarting WFB...");
        EventSubscriptionService.Publish<TabMessageEvent, string>("Restart Pushed");

        var power58GHz = 0;
        var power24GHz = 0;
        
        //wfb.conf has seperate values for 2.4ghz and 5ghz 
        if (SelectedChannel < 30)
        {
            // 2.4
            power24GHz = SelectedPower;
        }
        else
        {
            // 5.8
            power58GHz = SelectedPower;
        }
        
        var updatedWfbConfContent = UpdateWfbConfContent(
            WfbConfContent,
            SelectedFrequency58String,
            SelectedFrequency24String,
            power58GHz,
            power24GHz,
            SelectedBandwidth,
            SelectedMcsIndex,
            SelectedStbc,
            SelectedLdpc,
            SelectedFecK,
            SelectedFecN,
            SelectedChannel
        );

        if (string.IsNullOrEmpty(updatedWfbConfContent))
        {
            await MessageBoxManager.GetMessageBoxStandard("Error", "WfbConfContent is empty").ShowAsync();
            return;
        }

        WfbConfContent = updatedWfbConfContent;

        Logger.Information($"Uploading new : {OpenIPC.WfbConfFileLoc}");
        await SshClientService.UploadFileStringAsync(DeviceConfig.Instance, OpenIPC.WfbConfFileLoc, WfbConfContent);

        UpdateUIMessage("Restarting Wfb");
        await SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.WfbRestartCommand);
        UpdateUIMessage("Restarting Wfb..done");
    }
    
    /**
     * Take the updated yaml content and upload it to the device
     */
    private async void RestartWfb()
    {

        //if wfb.yaml, save that part and restart
        if (_globalSettingsService.IsWfbYamlEnabled)
        {
            Log.Information("Saving wfb.yaml...");
            
            try
            {
                var updatedYamlContent = _yamlConfigService.UpdateYaml(_yamlConfig);
                
                await SshClientService.UploadFileStringAsync(
                    DeviceConfig.Instance,
                    OpenIPC.WfbYamlFileLoc,
                    updatedYamlContent);

                SshClientService.ExecuteCommandAsync(
                    DeviceConfig.Instance,
                    DeviceCommands.WfbRestartCommand);

                Logger.Information("wfb.yaml configuration updated and service is restarting.");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to update Wfb.yaml configuration: {ExceptionMessage}", ex.Message);
                return;
            }
            
        }
        else
        {
            Log.Information("Saving legacy wfb.conf...");
            await updateWfbConfContent();
        }
    }
    #endregion

    #region Helper Methods

    private void ParseWfbYamlContent()
    {
        Debug.WriteLine("ParseWfbYamlContent");
        UpdateViewModelPropertiesFromYaml();
    }
    
    private void UpdateViewModelPropertiesFromYaml()
    {
        if (_yamlConfig.TryGetValue(WfbYaml.WfbChannel, out var channel)) HandleFrequencyKey(channel);

        if (_yamlConfig.TryGetValue(WfbYaml.WfbTxPower, out var power))
        {
            SelectedPower = TryParseInt(power, SelectedPower);    
        }

        if (_yamlConfig.TryGetValue(WfbYaml.WfbBandwidth, out var bandwidth))
        {
            SelectedBandwidth = TryParseInt(bandwidth, SelectedBandwidth);
        }

        if (_yamlConfig.TryGetValue(WfbYaml.BroadcastMcsIndex, out var wfbIndex))
        {
            SelectedMcsIndex = TryParseInt(wfbIndex, SelectedMcsIndex);
        }
        
        if (_yamlConfig.TryGetValue(WfbYaml.BroadcastFecK, out var fecK))
        {
            SelectedFecK = TryParseInt(fecK, SelectedFecK);
        }
        
        if (_yamlConfig.TryGetValue(WfbYaml.BroadcastFecN, out var fecN))
        {
            SelectedFecN = TryParseInt(fecN, SelectedFecN);
        }
        
        if (_yamlConfig.TryGetValue(WfbYaml.BroadcastLdpc, out var ldpc))
        {
            SelectedLdpc = TryParseInt(ldpc, SelectedLdpc);
        }
        
        if (_yamlConfig.TryGetValue(WfbYaml.BroadcastStbc, out var stbc))
        {
            SelectedStbc = TryParseInt(stbc, SelectedStbc);
        }
        if (_yamlConfig.TryGetValue(WfbYaml.WfbMlink, out var mlink))
        {
            Mlink = TryParseInt(mlink, SelectedStbc);
        }
    }
    private void ParseWfbConfContent()
    {
        if (string.IsNullOrEmpty(WfbConfContent))
        {
            Logger.Debug("WfbConfContent is empty.");
            return;
        }

        var lines = WfbConfContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            MapWfbKeyToProperty(key, value);
        }
    }

    private void MapWfbKeyToProperty(string key, string value)
    {
        switch (key)
        {
            case Wfb.Frequency:
                HandleFrequencyKey(value);
                break;
            case Wfb.Txpower:
                if(SelectedChannel < 30)
                    SelectedPower = TryParseInt(value, SelectedPower);
                break;
            case Wfb.DriverTxpowerOverride:
                if(SelectedChannel > 30)
                    SelectedPower = TryParseInt(value, SelectedPower);
                break;
            case Wfb.Bandwidth:
                SelectedBandwidth = TryParseInt(value, SelectedBandwidth);
                break;
            case Wfb.McsIndex:
                SelectedMcsIndex = TryParseInt(value, SelectedMcsIndex);
                break;
            case Wfb.Ldpc:
                SelectedLdpc = TryParseInt(value, SelectedLdpc);
                break;
            case Wfb.Stbc:
                SelectedStbc = TryParseInt(value, SelectedStbc);
                break;
            case Wfb.FecK:
                SelectedFecK = TryParseInt(value, SelectedFecK);
                break;
            case Wfb.FecN:
                SelectedFecN = TryParseInt(value, SelectedFecN);
                break;
            case Wfb.Channel:
                SelectedChannel = TryParseInt(value, SelectedChannel);
                HandleFrequencyKey(value);
                break;
        }
    }

    private void HandleFrequencyKey(string value)
    {
        if (int.TryParse(value, out var frequency))
        {
            SelectedFrequency58String = _58FrequencyMapping.ContainsKey(frequency)
                ? _58FrequencyMapping[frequency]
                : SelectedFrequency58String;

            SelectedFrequency24String = _24FrequencyMapping.ContainsKey(frequency)
                ? _24FrequencyMapping[frequency]
                : SelectedFrequency24String;
        }
    }

    private int TryParseInt(string value, int fallback)
    {
        return int.TryParse(value, out var result) ? result : fallback;
    }

    private void HandleFrequencyChange(string newValue, Dictionary<int, string> frequencyMapping)
    {
        // Reset the other frequency collection to its first value
        if (frequencyMapping == _24FrequencyMapping)
        {
            SelectedFrequency58String = Frequencies58GHz.FirstOrDefault();
            //SelectedPower = Power58GHz.FirstOrDefault();
        }
        else if (frequencyMapping == _58FrequencyMapping)
        {
            SelectedFrequency24String = Frequencies24GHz.FirstOrDefault();
            //SelectedPower = Power24GHz.FirstOrDefault();
        }

        // Extract the channel number
        var match = Regex.Match(newValue, @"\[(\d+)\]");
        SelectedChannel = match.Success && int.TryParse(match.Groups[1].Value, out var channel)
            ? channel
            : -1;
    }

    private string UpdateWfbConfContent(
        string wfbConfContent,
        string newFrequency58,
        string newFrequency24,
        int newPower58,
        int newPower24,
        int newBandwidth,
        int newMcsIndex,
        int newStbc,
        int newLdpc,
        int newFecK,
        int newFecN,
        int newChannel)
    {
        var regex = new Regex(
            @"^(?!#.*)(frequency|channel|driver_txpower_override|frequency24|bandwidth|txpower|mcs_index|stbc|ldpc|fec_k|fec_n)=.*",
            RegexOptions.Multiline);

        return regex.Replace(wfbConfContent, match =>
        {
            var key = match.Groups[1].Value;
            Logger.Debug($"Updating key: {key}");

            return key switch
            {
                "channel" => $"channel={newChannel}",
                "driver_txpower_override" => $"driver_txpower_override={newPower58}",
                "txpower" => $"txpower={newPower24}",
                "bandwidth" => $"bandwidth={newBandwidth}",
                "mcs_index" => $"mcs_index={newMcsIndex}",
                "stbc" => $"stbc={newStbc}",
                "ldpc" => $"ldpc={newLdpc}",
                "fec_k" => $"fec_k={newFecK}",
                "fec_n" => $"fec_n={newFecN}",
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