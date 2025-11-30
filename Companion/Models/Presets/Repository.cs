using System;
using System.Linq;

namespace Companion.Models.Presets;

/// <summary>
/// Represents a GitHub repository source for presets
/// </summary>
public class Repository
{
    /// <summary>
    /// The name of the repository (e.g., "OpenIPC Presets").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The full URL of the repository (e.g., "https://github.com/openipc/presets").
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// The owner of the repository (e.g., "openipc").
    /// </summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>
    /// The name of the repository without the owner (e.g., "presets").
    /// </summary>
    public string RepositoryName { get; set; } = string.Empty;

    /// <summary>
    /// The branch to use for fetching presets (default is "main").
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Indicates whether the repository is active (used for fetching presets).
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of when the repository was added.
    /// </summary>
    public DateTime AddedOn { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional description of the repository.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parses a GitHub repository URL and populates repository details
    /// </summary>
    /// <param name="url">The full GitHub repository URL</param>
    /// <returns>A populated Repository instance</returns>
    public static Repository FromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            
            // GitHub URLs are in the format: https://github.com/owner/repo
            // So we need to extract the owner and repo name from the path segments
            var pathSegments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathSegments.Length < 2)
            {
                throw new ArgumentException("Invalid GitHub repository URL. Expected format: https://github.com/owner/repo");
            }

            var owner = pathSegments[0];
            var repoName = pathSegments[1];

            return new Repository
            {
                Url = url,
                Owner = owner,
                RepositoryName = repoName,
                Name = $"{owner}/{repoName}",
                IsActive = true,
                AddedOn = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Error parsing repository URL: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates the raw content URL for the PRESET_INDEX.yaml
    /// </summary>
    /// <returns>URL to the PRESET_INDEX.yaml file</returns>
    public string GetPresetIndexUrl()
    {
        return $"https://raw.githubusercontent.com/{Owner}/{RepositoryName}/{Branch}/PRESET_INDEX.yaml";
    }

    /// <summary>
    /// Generates a download URL for a specific preset
    /// </summary>
    /// <param name="presetPath">Relative path to the preset</param>
    /// <returns>URL to download the preset configuration</returns>
    public string GetPresetDownloadUrl(string presetPath)
    {
        return $"https://raw.githubusercontent.com/{Owner}/{RepositoryName}/{Branch}/{presetPath}/preset-config.yaml";
    }
}