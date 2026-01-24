using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;

namespace Companion.Services;

public class UpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly string _latestJsonUrl;

    public UpdateChecker(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _latestJsonUrl = configuration["UpdateChecker:LatestJsonUrl"] ?? string.Empty;
    }


    public async Task<(bool HasUpdate, string ReleaseNotes, string DownloadUrl, string NewVersion)> CheckForUpdateAsync(
        string currentVersion)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_latestJsonUrl);
            var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(response);

            if (updateInfo != null && IsNewerVersion(updateInfo.Version, currentVersion))
                return (true, updateInfo.ReleaseNotes, updateInfo.DownloadUrl, updateInfo.Version);
        }
        catch (Exception ex)
        {
            Log.Error($"Error during update check: {ex.Message}");
        }

        return (false, string.Empty, string.Empty, string.Empty);
    }

    private bool IsNewerVersion(string newVersion, string currentVersion)
    {
        // Helper function to remove the "-v" prefix and extract the version number
        string ExtractVersionNumber(string version)
        {
            const string prefix = "v";
            return version.StartsWith(prefix) ? version.Substring(prefix.Length) : version;
        }

        // Helper function to remove the "release-v" prefix and extract the version number
        string ExtractGHVersionNumber(string version)
        {
            const string prefix = "release-v";
            return version.StartsWith(prefix) ? version.Substring(prefix.Length) : version;
        }

        string NormalizeVersion(string version)
        {
            var cleaned = ExtractVersionNumber(ExtractGHVersionNumber(version));
            var withoutMetadata = cleaned.Split('+', 2)[0];
            return withoutMetadata.Split('-', 2)[0];
        }

        // Extract and parse the version numbers
        return Version.TryParse(NormalizeVersion(newVersion), out var newVer) &&
               Version.TryParse(NormalizeVersion(currentVersion), out var currVer) &&
               newVer > currVer;
    }

    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;

        [JsonProperty("release_notes")] public string ReleaseNotes { get; set; } = string.Empty;

        [JsonProperty("download_url")] public string DownloadUrl { get; set; } = string.Empty;
    }
}
