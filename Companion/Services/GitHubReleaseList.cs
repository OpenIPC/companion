using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Companion.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Companion.Services;

public sealed record FirmwareReleaseOption(string Label, string Tag);

/// <summary>
/// Builds the selectable release list for channels using the GitHubReleaseList distribution.
/// </summary>
public static class GitHubReleaseList
{
    public const string Distribution = "GitHubReleaseList";
    private const int PageSize = 30;

    public static string BuildReleasesUrl(FirmwareChannelDefinition channel) =>
        $"https://api.github.com/repos/{channel.Owner}/{channel.Repository}/releases?per_page={PageSize}";

    public static IReadOnlyList<FirmwareReleaseOption> Parse(string json, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        JToken token;
        try
        {
            using var reader = new JsonTextReader(new StringReader(json))
            {
                DateParseHandling = DateParseHandling.DateTimeOffset
            };
            token = JToken.ReadFrom(reader);
        }
        catch (JsonException)
        {
            return [];
        }

        if (token is not JArray releases)
            return [];

        var published = releases
            .Where(release => release.Value<bool?>("draft") != true && release.Value<bool?>("prerelease") != true)
            .Select(release => new
            {
                Tag = release.Value<string>("tag_name"),
                PublishedAt = release["published_at"]?.Type == JTokenType.Date
                    ? release.Value<DateTimeOffset>("published_at")
                    : (DateTimeOffset?)null
            })
            .Where(release => !string.IsNullOrWhiteSpace(release.Tag) && release.PublishedAt.HasValue)
            .OrderByDescending(release => release.PublishedAt)
            .ToList();

        return published
            .Select((release, index) =>
            {
                DateTimeOffset local = TimeZoneInfo.ConvertTime(release.PublishedAt!.Value, timeZone);
                string label = $"{local:yyyy-MM-dd HH:mm} · {release.Tag}";
                return new FirmwareReleaseOption(index == 0 ? $"{label} (latest)" : label, release.Tag!);
            })
            .ToList();
    }
}
