using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Companion.Services;

public static class FirmwareSourceSettingsMigration
{
    /// <summary>
    /// Switches the previous default Mabur channel (latest gilankpam/openipc-builder release) to the
    /// release list. Channels pinned to a tag or pointing elsewhere are left as configured.
    /// </summary>
    public static bool UpgradeMaburReleaseList(JArray sources)
    {
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

        return legacyChannels.Count > 0;
    }

    private static bool Matches(JObject channel, string property, string expected, bool isDefault = false)
    {
        string? value = channel[property]?.ToString();
        if (value == null)
            return isDefault;

        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
