using Companion.Services;
using Newtonsoft.Json.Linq;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class FirmwareSourceSettingsMigrationTests
{
    private static JArray CreateSources(string distribution, string releaseTag = "latest") =>
        JArray.Parse($$"""
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
            """);

    [Test]
    public void UpgradeMaburReleaseList_PreviousDefault_SwitchesToReleaseList()
    {
        JArray sources = CreateSources("GitHubRelease");

        bool changed = FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(sources);

        Assert.That(changed, Is.True);
        Assert.That(sources[1]["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubReleaseList"));
        Assert.That(sources[0]["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubRelease"));
    }

    [Test]
    public void UpgradeMaburReleaseList_PinnedReleaseTag_LeavesChannelAlone()
    {
        JArray sources = CreateSources("GitHubRelease", "mabur-05fad388");

        bool changed = FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(sources);

        Assert.That(changed, Is.False);
        Assert.That(sources[1]["Channels"]![0]!["Distribution"]!.ToString(), Is.EqualTo("GitHubRelease"));
    }

    [Test]
    public void UpgradeMaburReleaseList_AlreadyUpgraded_ReportsNoChange()
    {
        JArray sources = CreateSources("GitHubReleaseList");

        Assert.That(FirmwareSourceSettingsMigration.UpgradeMaburReleaseList(sources), Is.False);
    }
}
