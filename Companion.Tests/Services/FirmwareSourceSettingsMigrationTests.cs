using Companion.Services;
using Newtonsoft.Json.Linq;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class FirmwareSourceSettingsMigrationTests
{
    private static JObject CreateSettings(string distribution, string releaseTag = "latest") =>
        new(new JProperty("FirmwareSources", JArray.Parse($$"""
            [
              {
                "Name": "OpenIPC Builder",
                "Track": "Stable",
                "Channels": [ { "Name": "Latest", "Distribution": "GitHubRelease", "Owner": "OpenIPC", "Repository": "builder", "ReleaseTag": "latest" } ]
              },
              {
                "Name": "Mabur",
                "Track": "Test",
                "Channels": [ { "Name": "Latest", "Distribution": "{{distribution}}", "Owner": "gilankpam", "Repository": "openipc-builder", "ReleaseTag": "{{releaseTag}}" } ]
              }
            ]
            """)));

    [Test]
    public void UpgradeMaburReleaseList_PreviousDefault_SwitchesToReleaseList()
    {
        JObject settings = CreateSettings("GitHubRelease");

        bool changed = FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(settings);

        Assert.That(changed, Is.True);
        Assert.That(settings["FirmwareSources"]![1]!["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubReleaseList"));
        Assert.That(settings["FirmwareSources"]![0]!["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubRelease"));
    }

    [Test]
    public void UpgradeMaburReleaseList_PinnedReleaseTag_LeavesChannelAlone()
    {
        JObject settings = CreateSettings("GitHubRelease", "mabur-05fad388");

        FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(settings);

        Assert.That(settings["FirmwareSources"]![1]!["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubRelease"));
    }

    [Test]
    public void UpgradeMaburReleaseList_AlreadyMigrated_ReportsNoChange()
    {
        JObject settings = CreateSettings("GitHubRelease");
        settings[FirmwareSourceSettingsMigration.MarkerKey] = true;

        Assert.That(FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(settings), Is.False);
        Assert.That(settings["FirmwareSources"]![1]!["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubRelease"));
    }

    [Test]
    public void UpgradeMaburReleaseList_RunsOnce_KeepsLaterHandEditedChannel()
    {
        JObject settings = CreateSettings("GitHubRelease");
        FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(settings);
        settings["FirmwareSources"]![1]!["Channels"]![0]!["Distribution"] = "GitHubRelease";

        bool changed = FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(settings);

        Assert.That(changed, Is.False);
        Assert.That(settings["FirmwareSources"]![1]!["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubRelease"));
    }
}
