using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Companion.Services;

public static class FirmwareSourceSettingsMigration
{
    public const string MarkerKey = "MaburReleaseListMigrated";

    /// <summary>
    /// Switches the previous default Mabur channel (latest gilankpam/openipc-builder release) to the
    /// release list, once per settings file. Channels pinned to a tag or pointing elsewhere are left as
    /// configured, and later hand edits are kept. Returns true when the settings changed.
    /// </summary>
    public static bool UpgradeMaburReleaseList(JObject settings)
    {
        if (settings[MarkerKey]?.Value<bool>() == true)
            return false;

        var sources = settings["FirmwareSources"] as JArray ?? new JArray();
        var legacyChannels = sources
            .SelectMany(source => source?["Channels"] as JArray ?? new JArray())
            .OfType<JObject>()
            .Where(channel =>
                Matches(channel, "Owner", "gilankpam") &&
                Matches(channel, "Repository", "openipc-builder") &&
                Matches(channel, "Distribution", "GitHubRelease", isDefault: true) &&
                Matches(channel, "ReleaseTag", "latest", isDefault: true))
            .ToList();

        foreach (JObject channel in legacyChannels)
            channel["Distribution"] = GitHubReleaseList.Distribution;

        settings[MarkerKey] = true;
        return true;
    }

    private static bool Matches(JObject channel, string property, string expected, bool isDefault = false)
    {
        string? value = channel[property]?.ToString();
        if (value == null)
            return isDefault;

        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
