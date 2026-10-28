using System;
using System.Linq;
using Companion.Models;
using Companion.Services;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class GitHubReleaseListTests
{
    private const string ReleasesJson = """
        [
          { "tag_name": "mabur-05fad388", "draft": false, "prerelease": false, "published_at": "2026-10-23T17:00:09Z" },
          { "tag_name": "mabur-draft", "draft": true, "prerelease": false, "published_at": null },
          { "tag_name": "mabur-4c94bc23", "draft": false, "prerelease": false, "published_at": "2026-10-23T19:00:27Z" },
          { "tag_name": "mabur-beta", "draft": false, "prerelease": true, "published_at": "2026-10-24T08:00:00Z" }
        ]
        """;

    [Test]
    public void Parse_ExcludesDraftsAndPrereleasesAndSortsNewestFirst()
    {
        var options = GitHubReleaseList.Parse(ReleasesJson, TimeZoneInfo.Utc);

        Assert.That(options.Select(option => option.Tag),
            Is.EqualTo(new[] { "mabur-4c94bc23", "mabur-05fad388" }));
    }

    [Test]
    public void Parse_LabelsWithPublishTimeInTimeZoneAndMarksNewestAsLatest()
    {
        var pacific = TimeZoneInfo.CreateCustomTimeZone("Test-0700", TimeSpan.FromHours(-7), "Test-0700", "Test-0700");

        var options = GitHubReleaseList.Parse(ReleasesJson, pacific);

        Assert.That(options.Select(option => option.Label), Is.EqualTo(new[]
        {
            "2026-10-23 12:00 · mabur-4c94bc23 (latest)",
            "2026-10-23 10:00 · mabur-05fad388"
        }));
    }

    [Test]
    public void Parse_InvalidOrEmptyResponse_ReturnsEmpty()
    {
        Assert.That(GitHubReleaseList.Parse(string.Empty, TimeZoneInfo.Utc), Is.Empty);
        Assert.That(GitHubReleaseList.Parse("{\"message\":\"API rate limit exceeded\"}", TimeZoneInfo.Utc), Is.Empty);
    }

    [Test]
    public void BuildReleasesUrl_UsesOwnerRepositoryAndPageSize()
    {
        var channel = new FirmwareChannelDefinition { Owner = "gilankpam", Repository = "openipc-builder" };

        Assert.That(GitHubReleaseList.BuildReleasesUrl(channel),
            Is.EqualTo("https://api.github.com/repos/gilankpam/openipc-builder/releases?per_page=30"));
    }
}
