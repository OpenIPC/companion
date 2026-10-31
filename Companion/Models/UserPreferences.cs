namespace Companion.Models;

public sealed class UserPreferences
{
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string PreferredFirmwareSource { get; set; } = "OpenIPC Builder";
    public string PreferredFirmwareTrack { get; set; } = "Stable";
    public string PreferredFirmwareChannel { get; set; } = "Latest";
    public bool FirmwareFocusedMode { get; set; } = true;
    public string LastSelectedTab { get; set; } = string.Empty;
    public bool IsTabsCollapsed { get; set; }
    public double LogPanelHeight { get; set; } = 60;
    public bool IsLogPanelCollapsed { get; set; }
}
