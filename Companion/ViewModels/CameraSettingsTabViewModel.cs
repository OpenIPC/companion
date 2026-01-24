using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
/// ViewModel for managing camera settings and configurations
/// </summary>
public partial class CameraSettingsTabViewModel : ViewModelBase
{
    #region Private Fields
    private readonly IEventSubscriptionService _eventSubscriptionService;
    private readonly Dictionary<string, string> _yamlConfig = new();
    private readonly IYamlConfigService _yamlConfigService;
    #endregion

    #region Observable Collections
    [ObservableProperty] private ObservableCollection<string> _resolution = new();
    [ObservableProperty] private ObservableCollection<string> _fps = new();
    [ObservableProperty] private ObservableCollection<string> _codec = new();
    [ObservableProperty] private ObservableCollection<string> _bitrate = new();
    [ObservableProperty] private ObservableCollection<string> _exposure = new();
    [ObservableProperty] private ObservableCollection<string> _contrast = new();
    [ObservableProperty] private ObservableCollection<string> _hue = new();
    [ObservableProperty] private ObservableCollection<string> _saturation = new();
    [ObservableProperty] private ObservableCollection<string> _luminance = new();
    [ObservableProperty] private ObservableCollection<string> _flip = new();
    [ObservableProperty] private ObservableCollection<string> _mirror = new();
    #endregion

    #region FPV Settings Collections
    [ObservableProperty] private ObservableCollection<string> _fpvEnabled = new();
    [ObservableProperty] private ObservableCollection<string> _fpvNoiseLevel = new();
    [ObservableProperty] private ObservableCollection<string> _fpvRoiQp = new();
    [ObservableProperty] private ObservableCollection<string> _fpvRefEnhance = new();
    [ObservableProperty] private ObservableCollection<string> _fpvRefPred = new();
    [ObservableProperty] private ObservableCollection<string> _fpvIntraLine = new();
    [ObservableProperty] private ObservableCollection<string> _fpvIntraQp = new();
    [ObservableProperty] private ObservableCollection<string> _fpvRoiRectLeft = new() { "" };
    [ObservableProperty] private ObservableCollection<string> _fpvRoiRectTop = new() { "" };
    [ObservableProperty] private ObservableCollection<string> _fpvRoiRectHeight = new() { "" };
    [ObservableProperty] private ObservableCollection<string> _fpvRoiRectWidth = new() { "" };
    #endregion

    #region Selected Values
    [ObservableProperty] private string _selectedResolution = string.Empty;
    [ObservableProperty] private string _selectedFps = string.Empty;
    [ObservableProperty] private string _selectedCodec = string.Empty;
    [ObservableProperty] private string _selectedBitrate = string.Empty;
    [ObservableProperty] private string _selectedExposure = string.Empty;
    [ObservableProperty] private string _selectedContrast = string.Empty;
    [ObservableProperty] private string _selectedHue = string.Empty;
    [ObservableProperty] private string _selectedSaturation = string.Empty;
    [ObservableProperty] private string _selectedLuminance = string.Empty;
    [ObservableProperty] private string _selectedFlip = string.Empty;
    [ObservableProperty] private string _selectedMirror = string.Empty;
    #endregion

    #region FPV Selected Values
    [ObservableProperty] private string _selectedFpvEnabled = string.Empty;
    [ObservableProperty] private string _selectedFpvNoiseLevel = string.Empty;
    [ObservableProperty] private string _selectedFpvRoiQp = string.Empty;
    [ObservableProperty] private string _selectedFpvRefEnhance = string.Empty;
    [ObservableProperty] private string _selectedFpvRefPred = string.Empty;
    [ObservableProperty] private string _selectedFpvIntraLine = string.Empty;
    [ObservableProperty] private string _selectedFpvIntraQp = string.Empty;
    [ObservableProperty] private string _combinedFpvRoiRectValue = string.Empty;
    #endregion

    #region Other Properties
    [ObservableProperty] private bool _canConnect;
    [ObservableProperty] private bool _isOnboardRecOn;
    [ObservableProperty] private bool _isAudioEnabled;
    [ObservableProperty] private bool _isNoTimeEnabled;
    
    #endregion

    #region Commands
    public ICommand RestartMajesticCommand { get; }
    #endregion

    #region Constructor
    /// <summary>
    /// Initializes a new instance of CameraSettingsTabViewModel
    /// </summary>
    public CameraSettingsTabViewModel(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IYamlConfigService yamlConfigService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        _yamlConfigService = yamlConfigService ?? throw new ArgumentNullException(nameof(yamlConfigService));
        _eventSubscriptionService = eventSubscriptionService ??
            throw new ArgumentNullException(nameof(eventSubscriptionService));

        InitializeCollections();
        RestartMajesticCommand = new AsyncRelayCommand(SaveRestartMajesticCommand);

        _eventSubscriptionService.Subscribe<MajesticContentUpdatedEvent, MajesticContentUpdatedMessage>(
            OnMajesticContentUpdated);
        _eventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessageEvent);
    }
    #endregion

    #region Property Change Handlers
    partial void OnSelectedResolutionChanged(string value)
    {
        Logger.Debug($"SelectedResolution updated to {value}");
        UpdateYamlConfig(Majestic.VideoSize, value);
    }

    partial void OnSelectedFpsChanged(string value)
    {
        Logger.Debug($"SelectedFps updated to {value}");
        UpdateYamlConfig(Majestic.VideoFps, value);
    }

    partial void OnSelectedCodecChanged(string value)
    {
        Logger.Debug($"SelectedCodec updated to {value}");
        UpdateYamlConfig(Majestic.VideoCodec, value);
    }

    partial void OnSelectedBitrateChanged(string value)
    {
        Logger.Debug($"SelectedBitrate updated to {value}");
        UpdateYamlConfig(Majestic.VideoBitrate, value);
    }

    partial void OnSelectedExposureChanged(string value)
    {
        Logger.Debug($"SelectedExposure updated to {value}");
        UpdateYamlConfig(Majestic.IspExposure, value);
    }

    partial void OnSelectedHueChanged(string value)
    {
        Logger.Debug($"SelectedHue updated to {value}");
        UpdateYamlConfig(Majestic.ImageHue, value);
    }

    partial void OnSelectedContrastChanged(string value)
    {
        Logger.Debug($"SelectedContrast updated to {value}");
        UpdateYamlConfig(Majestic.ImageContrast, value);
    }

    partial void OnSelectedSaturationChanged(string value)
    {
        Logger.Debug($"SelectedSaturation updated to {value}");
        UpdateYamlConfig(Majestic.ImageSaturation, value);
    }

    partial void OnSelectedLuminanceChanged(string value)
    {
        Logger.Debug($"SelectedLuminance updated to {value}");
        UpdateYamlConfig(Majestic.ImageLuminance, value);
    }

    partial void OnSelectedFlipChanged(string value)
    {
        Logger.Debug($"SelectedFlip updated to {value}");
        UpdateYamlConfig(Majestic.ImageFlip, value.ToLower());
    }

    partial void OnSelectedMirrorChanged(string value)
    {
        Logger.Debug($"SelectedMirror updated to {value}");
        UpdateYamlConfig(Majestic.ImageMirror, value.ToLower());
    }

    partial void OnSelectedFpvEnabledChanged(string value)
    {
        Logger.Debug($"SelectedFpvEnabledChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvEnabled, value.ToLower());
    }

    partial void OnIsOnboardRecOnChanged(bool value)
    {
        Logger.Information($"Onboard recording toggled to: {value}");
        _yamlConfig[Majestic.RecordsEnabled] = value.ToString().ToLower();
    }
    
    partial void OnIsNoTimeEnabledChanged(bool value)
    {
        Logger.Information($"Notime recording toggled to: {value}");
        _yamlConfig[Majestic.RecordsNoTime] = value.ToString().ToLower();
    }
    
    partial void OnIsAudioEnabledChanged(bool value)
    {
        Logger.Information($"audio toggled to: {value}");
        _yamlConfig[Majestic.AudioEnabled] = value.ToString().ToLower();
    }

    partial void OnSelectedFpvNoiseLevelChanged(string value)
    {
        Logger.Debug($"SelectedFpvNoiseLevelChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvNoiseLevel, value);
    }

    partial void OnSelectedFpvRoiQpChanged(string value)
    {
        Logger.Debug($"SelectedFpvRoiQpChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvRoiQp, value);
    }

    partial void OnSelectedFpvRefEnhanceChanged(string value)
    {
        Logger.Debug($"SelectedFpvRefEnhanceChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvRefEnhance, value);
    }

    partial void OnSelectedFpvRefPredChanged(string value)
    {
        Logger.Debug($"SelectedFpvRefPredChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvRefPred, value);
    }

    partial void OnSelectedFpvIntraLineChanged(string value)
    {
        Logger.Debug($"SelectedFpvIntraLineChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvIntraLine, value);
    }

    partial void OnSelectedFpvIntraQpChanged(string value)
    {
        Logger.Debug($"SelectedFpvIntraQpChanged updated to {value}");
        UpdateYamlConfig(Majestic.FpvIntraQp, value);
    }
    #endregion

    #region Event Handlers
    private void OnAppMessageEvent(AppMessage appMessage)
    {
        CanConnect = appMessage.CanConnect;
    }

    public void OnMajesticContentUpdated(MajesticContentUpdatedMessage message)
    {
        Logger.Debug("Processing MajesticContentUpdatedMessage.");
        _yamlConfigService.ParseYaml(message.Content, _yamlConfig);
        UpdateViewModelPropertiesFromYaml();
    }
    #endregion

    #region Private Methods
    private void InitializeCollections()
    {
        Resolution = new ObservableCollection<string>
        {
            "1280x720", "1456x816", "1920x1080", "2104x1184", "2208x1248", "2240x1264", "2312x1304",
            "2512x1416", "2560x1440", "2560x1920", "3200x1800", "3840x2160"
        };

        Fps = new ObservableCollection<string>
        {
            "20", "30", "40", "50", "60", "70", "80", "90", "100", "110", "120"
        };

        Codec = new ObservableCollection<string> { "h264", "h265" };
        Bitrate = new ObservableCollection<string>(Enumerable.Range(1, 40).Select(i => (i * 1024).ToString()));
        Exposure = new ObservableCollection<string> { "5", "6", "8", "10", "11", "12", "14", "16", "33", "50" };

        Contrast = new ObservableCollection<string>(Enumerable.Range(1, 100).Select(i => (i * 5).ToString()));
        Hue = new ObservableCollection<string>(Enumerable.Range(1, 100).Select(i => (i * 5).ToString()));
        Saturation = new ObservableCollection<string>(Enumerable.Range(1, 100).Select(i => (i * 5).ToString()));
        Luminance = new ObservableCollection<string>(Enumerable.Range(1, 100).Select(i => (i * 5).ToString()));

        Flip = new ObservableCollection<string> { "true", "false" };
        SelectedFlip = "false";

        Mirror = new ObservableCollection<string> { "true", "false" };
        SelectedMirror = "false";

        FpvEnabled = new ObservableCollection<string> { "true", "false" };
        SelectedFpvEnabled = "false";

        FpvNoiseLevel = new ObservableCollection<string> { "", "0", "1", "2" };

        FpvRoiQp = new ObservableCollection<string>(Enumerable.Range(-30, 61).Select(i => i.ToString()));
        FpvRoiQp.Insert(0, "");

        FpvRefEnhance = new ObservableCollection<string>(Enumerable.Range(0, 10).Select(i => i.ToString()));
        FpvRefEnhance.Insert(0, "");

        FpvRefPred = new ObservableCollection<string> { "", "true", "false" };

        FpvIntraLine = new ObservableCollection<string>(Enumerable.Range(0, 10).Select(i => i.ToString()));
        FpvIntraLine.Insert(0, "");

        FpvIntraQp = new ObservableCollection<string> { "", "true", "false" };

        FpvRoiRectLeft = new ObservableCollection<string> { "" };
    }

    private void UpdateCombinedValue()
    {
        var fpvRoiRectLeft = FpvRoiRectLeft[0];
        var fpvRoiRectTop = FpvRoiRectTop[0];
        var fpvRoiRectHeight = FpvRoiRectHeight[0];
        var fpvRoiRectWidth = FpvRoiRectWidth[0];

        CombinedFpvRoiRectValue = string.IsNullOrEmpty(fpvRoiRectLeft) &&
                                 string.IsNullOrEmpty(fpvRoiRectTop) &&
                                 string.IsNullOrEmpty(fpvRoiRectHeight) &&
                                 string.IsNullOrEmpty(fpvRoiRectWidth)
            ? ""
            : $"{FpvRoiRectLeft[0]}x{FpvRoiRectTop[0]}x{FpvRoiRectHeight[0]}x{FpvRoiRectWidth[0]}";

        Logger.Debug($"Combined value updated to {CombinedFpvRoiRectValue}");
        UpdateYamlConfig(Majestic.FpvRoiRect, CombinedFpvRoiRectValue);
    }

    private void UpdateViewModelPropertiesFromYaml()
    {
        if (_yamlConfig.TryGetValue(Majestic.VideoSize, out var resolution)) SelectedResolution = resolution;

        if (_yamlConfig.TryGetValue(Majestic.VideoFps, out var fps)) SelectedFps = fps;

        if (_yamlConfig.TryGetValue(Majestic.VideoCodec, out var codec)) SelectedCodec = codec;

        if (_yamlConfig.TryGetValue(Majestic.VideoBitrate, out var bitrate)) SelectedBitrate = bitrate;

        if (_yamlConfig.TryGetValue(Majestic.IspExposure, out var exposure)) SelectedExposure = exposure;

        if (_yamlConfig.TryGetValue(Majestic.ImageContrast, out var contrast)) SelectedContrast = contrast;

        if (_yamlConfig.TryGetValue(Majestic.ImageHue, out var hue)) SelectedHue = hue;

        if (_yamlConfig.TryGetValue(Majestic.ImageSaturation, out var saturation)) SelectedSaturation = saturation;

        if (_yamlConfig.TryGetValue(Majestic.ImageLuminance, out var luminance)) SelectedLuminance = luminance;

        if (_yamlConfig.TryGetValue(Majestic.ImageFlip, out var flip)) SelectedFlip = flip;

        if (_yamlConfig.TryGetValue(Majestic.ImageMirror, out var mirror)) SelectedMirror = mirror;

        if (_yamlConfig.TryGetValue(Majestic.RecordsEnabled, out var onboardRecValue) &&
            bool.TryParse(onboardRecValue?.ToString(), out var isOnboardRec))
        {
            IsOnboardRecOn = isOnboardRec;
        }
        else
        {
            IsOnboardRecOn = false;
        }

        if (_yamlConfig.TryGetValue(Majestic.FpvEnabled, out var fpvEnabled)) SelectedFpvEnabled = fpvEnabled;

        if (_yamlConfig.TryGetValue(Majestic.FpvNoiseLevel, out var fpvNoiseLevel))
            SelectedFpvNoiseLevel = fpvNoiseLevel;

        if (_yamlConfig.TryGetValue(Majestic.FpvRoiQp, out var fpvRoiQp)) SelectedFpvRoiQp = fpvRoiQp;

        if (_yamlConfig.TryGetValue(Majestic.FpvRefEnhance, out var fpvRefEnhance))
            SelectedFpvRefEnhance = fpvRefEnhance;

        if (_yamlConfig.TryGetValue(Majestic.FpvRefPred, out var fpvRefPred)) SelectedFpvRefPred = fpvRefPred;

        if (_yamlConfig.TryGetValue(Majestic.FpvIntraLine, out var fpvIntraLine)) SelectedFpvIntraLine = fpvIntraLine;

        if (_yamlConfig.TryGetValue(Majestic.FpvIntraQp, out var fpvIntraQp)) SelectedFpvIntraQp = fpvIntraQp;

        if (_yamlConfig.TryGetValue(Majestic.FpvRoiRect, out var fpvRoiRect))
        {
            var parts = fpvRoiRect.Split('x');
            if (parts.Length == 4)
            {
                if (FpvRoiRectLeft.Count > 0) FpvRoiRectLeft[0] = parts[0];
                if (FpvRoiRectTop.Count > 0) FpvRoiRectTop[0] = parts[1];
                if (FpvRoiRectHeight.Count > 0) FpvRoiRectHeight[0] = parts[2];
                if (FpvRoiRectWidth.Count > 0) FpvRoiRectWidth[0] = parts[3];
            }
            else
            {
                Logger.Warning($"Invalid format for FpvRoiRect value: {fpvRoiRect}");
            }
        }
        
        if (_yamlConfig.TryGetValue(Majestic.AudioEnabled, out var audioEnabledValue) &&
            bool.TryParse(audioEnabledValue?.ToString(), out var audioEnabled))
        {
            IsAudioEnabled = audioEnabled;
        }
        else
        {
            IsAudioEnabled = false;
        }
        
        if (_yamlConfig.TryGetValue(Majestic.RecordsNoTime, out var recordsNoTimeValue) &&
            bool.TryParse(recordsNoTimeValue?.ToString(), out var recordsNoTime))
        {
            IsNoTimeEnabled = recordsNoTime;
        }
        else
        {
            IsNoTimeEnabled = false;
        }
    }
    #endregion

    #region Public Methods
    public void UpdateYamlConfig(string key, string newValue)
    {
        if (_yamlConfig.ContainsKey(key))
            _yamlConfig[key] = newValue;
        else
            _yamlConfig.Add(key, newValue);

        if (string.IsNullOrEmpty(newValue))
            _yamlConfig.Remove(key);
    }

    public async Task SaveRestartMajesticCommand()
    {
        Logger.Debug("Preparing to Save Majestic YAML file.");

        UpdateCombinedValue();
        var updatedYamlContent = _yamlConfigService.UpdateYaml(_yamlConfig);

        try
        {
            await SshClientService.UploadFileStringAsync(
                DeviceConfig.Instance,
                OpenIPC.MajesticFileLoc,
                updatedYamlContent);

            await SshClientService.ExecuteCommandAsync(
                DeviceConfig.Instance,
                DeviceCommands.MajesticRestartCommand);

            Logger.Information("Majestic configuration updated and service is restarting.");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to update Majestic configuration: {ExceptionMessage}", ex.Message);
        }
    }
    #endregion
}
