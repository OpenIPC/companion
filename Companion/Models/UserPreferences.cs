namespace Companion.Models;

public sealed class UserPreferences
{
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public bool AutoDiscoverOnStartup { get; set; } = true;
    public bool AutoConnectOnDiscovery { get; set; } = true;
    public string PreferredFirmwareSource { get; set; } = "OpenIPC Builder";
    public bool FirmwareFocusedMode { get; set; } = true;
    public string LastSelectedTab { get; set; } = string.Empty;
    public bool IsTabsCollapsed { get; set; }
}
