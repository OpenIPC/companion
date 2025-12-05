using System;

namespace Companion.Models.Presets;

public class GitHubFile
{
    /// <summary>
    /// The name of the file (e.g., "preset-config.yaml").
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The relative path of the file in the repository (e.g., "presets/preset-config.yaml").
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// The URL to directly download the file content.
    /// </summary>
    public string DownloadUrl { get; set; }

    
}