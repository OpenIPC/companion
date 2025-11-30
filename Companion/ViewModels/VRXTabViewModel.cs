using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Companion.Events;
using Companion.Models;
using Companion.Services;
using Serilog;

namespace Companion.ViewModels;

/// <summary>
///     Interaction logic for VRXTabViewModel.xaml
///     Parses:
///     echo y | pscp -scp -pw %3 root@%2:/etc/wifibroadcast.cfg .
///     echo y | pscp -scp -pw %3 root@%2:/etc/modprobe.d/wfb.conf .
///     echo y | pscp -scp -pw %3 root@%2:/home/radxa/scripts/screen-mode .
/// </summary>
public partial class VRXTabViewModel : ViewModelBase
{
    [ObservableProperty] private bool _canConnect;

    [ObservableProperty] private string _droneKeyChecksum;

    [ObservableProperty] private ObservableCollection<string> _fps;
    [ObservableProperty] private bool _isExtendedMavLinkOSD;

    [ObservableProperty] private bool _isSimpleMavLinkOSD;

    [ObservableProperty] private ObservableCollection<string> _resolution;

    [ObservableProperty] private string _selectedFps;

    [ObservableProperty] private string _selectedResolution;

    [ObservableProperty] private string _wfbConfContent;

    public VRXTabViewModel(ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService)
        : base(logger, sshClientService, eventSubscriptionService)
    {
        EventSubscriptionService.Subscribe<DeviceTypeChangeEvent, DeviceType>(onDeviceTypeChangeEvent);
        EventSubscriptionService.Subscribe<AppMessageEvent, AppMessage>(OnAppMessage);
        EventSubscriptionService.Subscribe<RadxaContentUpdateChangeEvent, RadxaContentUpdatedMessage>(
            OnRadxaContentUpdateChange);

        InitializeCollections();
    }


    private void OnAppMessage(AppMessage appMessage)
    {
        if (appMessage.CanConnect) CanConnect = appMessage.CanConnect;
        //Log.Information($"CanConnect {CanConnect.ToString()}");
    }

    private void onDeviceTypeChangeEvent(DeviceType deviceType)
    {
        //throw new System.NotImplementedException();
    }


    private void InitializeCollections()
    {
        Resolution = new ObservableCollection<string>
        {
            "1280x720", "1920x1080"
        };

        Fps = new ObservableCollection<string>
        {
            "20", "30", "40", "50", "60", "70", "80", "90", "100", "110", "120"
        };
    }

    private void OnRadxaContentUpdateChange(RadxaContentUpdatedMessage radxaContentUpdatedMessage)
    {
        Log.Debug("Parsing radxaContentUpdatedMessage.");
        if (!string.IsNullOrEmpty(radxaContentUpdatedMessage.WifiBroadcastContent))
        {
            var configContent = radxaContentUpdatedMessage.WifiBroadcastContent;
            var parser = new WifiConfigParser();
            parser.ParseConfigString(configContent);

            var channel = parser.WifiChannel;
        }

        if (!string.IsNullOrEmpty(radxaContentUpdatedMessage.ScreenModeContent))
        {
            // value comes across as 1920x1080@60
            var screenMode = radxaContentUpdatedMessage.ScreenModeContent;
            var cleanScreenMode = Utilities.RemoveSpecialCharacters(screenMode);
            var screenModeSplit = cleanScreenMode.Split('@');

            var resolution = screenModeSplit[0];
            var fps = screenModeSplit[1];

            if (Fps.Contains(fps)) SelectedFps = fps;
            if (Resolution.Contains(resolution)) SelectedResolution = resolution;
        }

        if (!string.IsNullOrEmpty(radxaContentUpdatedMessage.WfbConfContent))
        {
            //WifiConfigParser parser = new WifiConfigParser();
            //parser.ParseConfigString(radxaContentUpdatedMessage.WfbConfContent);
        }

        if (!string.IsNullOrEmpty(radxaContentUpdatedMessage.DroneKeyContent))
        {
            var droneKey = radxaContentUpdatedMessage.DroneKeyContent;
            DroneKeyChecksum = droneKey;
        }
    }

    [RelayCommand]
    private async Task RestartWfb()
    {
        var resolution = SelectedResolution;
        var fps = SelectedFps;

        //format 1920x1080@60
        var screenMode = $"{resolution}@{fps}\n";

        UpdateUIMessage("Uploading screen mode");
        await SshClientService.UploadFileStringAsync(DeviceConfig.Instance, OpenIPC.ScreenModeFileLoc, screenMode);

        // update the files here
        // VRX
        // Resolution /config/scripts/screen-mode
        // FPS /config/scripts/screen-mode
        // contents of /etc/default/wifibroadcast
    }


    [RelayCommand]
    private async Task EnableVrxMajestic()
    {
        if (IsSimpleMavLinkOSD)
            try
            {
                //basic
                UpdateUIMessage("Setting Simple Mavlink OSD");
                SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.GsMavBasic1);
                SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.GsMavBasic2);
                UpdateUIMessage("Rebooting device");
                SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.RebootCommand);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                throw;
            }
        else
            //extended
            try
            {
                UpdateUIMessage("Setting Extended Mavlink OSD");
                SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.GsMavExtended1);
                SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.GsMavExtended2);
                UpdateUIMessage("Rebooting device");
                SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.RebootCommand);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                throw;
            }

        //rBtnMode1 = Extended,mavgs
        //rBtnMode2 = Simple, mavgs2
        //if "%1" == "mavgs" (
        // 	plink -ssh root@%2 -pw %3 sed -i '/pixelpilot --osd --osd-elements video,wfbng --screen-mode $SCREEN_MODE --dvr-framerate $REC_FPS --dvr-fmp4 --dvr record_${current_date}.mp4 "&"/c\pixelpilot --osd --screen-mode $SCREEN_MODE --dvr-framerate $REC_FPS --dvr-fmp4 --dvr record_${current_date}.mp4 --osd-telem-lvl 2 "&"' /config/scripts/stream.sh
        // 	plink -ssh root@%2 -pw %3 sed -i '/pixelpilot --osd --osd-elements video,wfbng --screen-mode $SCREEN_MODE "&"/c\pixelpilot --osd --screen-mode $SCREEN_MODE --osd-telem-lvl 2 "&"' /config/scripts/stream.sh
        //         plink -ssh root@%2 -pw %3 reboot
        // )
        // 
        // if "%1" == "mavgs2" (
        // 	plink -ssh root@%2 -pw %3 sed -i '/pixelpilot --osd --osd-elements video,wfbng --screen-mode $SCREEN_MODE --dvr-framerate $REC_FPS --dvr-fmp4 --dvr record_${current_date}.mp4 "&"/c\pixelpilot --osd --screen-mode $SCREEN_MODE --dvr-framerate $REC_FPS --dvr-fmp4 --dvr record_${current_date}.mp4 --osd-telem-lvl 1 "&"' /config/scripts/stream.sh
        // 	plink -ssh root@%2 -pw %3 sed -i '/pixelpilot --osd --osd-elements video,wfbng --screen-mode $SCREEN_MODE "&"/c\pixelpilot --osd --screen-mode $SCREEN_MODE --osd-telem-lvl 1 "&"' /config/scripts/stream.sh
        //         plink -ssh root@%2 -pw %3 reboot
        // )
    }

    [RelayCommand]
    private async Task EnableVrxMSPDisplayport()
    {
        Log.Information("EnableVrxMSPDisplayport clicked");
        SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.GSMSPDisplayportCommand);
        SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.GSMSPDisplayport2Command);
        SshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.RebootCommand);
        Log.Information("EnableVrxMSPDisplaypor..done");
        //mspgs
        //plink -ssh root@%2 -pw %3 sed -i '/pixelpilot --osd --screen-mode $SCREEN_MODE --dvr-framerate $REC_FPS --dvr-fmp4 --dvr record_${current_date}.mp4/c\pixelpilot --osd --osd-elements video,wfbng --screen-mode $SCREEN_MODE --dvr-framerate $REC_FPS --dvr-fmp4 --dvr record_${current_date}.mp4 "&"' /config/scripts/stream.sh
        //plink -ssh root@%2 -pw %3 sed -i '/pixelpilot --osd --screen-mode $SCREEN_MODE/c\pixelpilot --osd --osd-elements video,wfbng --screen-mode $SCREEN_MODE "&"' /config/scripts/stream.sh
        //plink -ssh root@%2 -pw %3 reboot
    }
}