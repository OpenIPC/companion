using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Companion.Models.Presets;

/// <summary>
/// Represents an individual preset entry in the index
/// </summary>
public class PresetIndexEntry
{
    /// <summary>
    /// Name of the preset
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Relative path to the preset in the repository
    /// </summary>
    [YamlMember(Alias = "path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Category of the preset
    /// </summary>
    [YamlMember(Alias = "category")]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Author of the preset
    /// </summary>
    [YamlMember(Alias = "author")]
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Description of the preset
    /// </summary>
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Tags associated with the preset
    /// </summary>
    [YamlMember(Alias = "tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Status of the preset (e.g., Community, Official, Draft)
    /// </summary>
    [YamlMember(Alias = "status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// List of files modified by this preset
    /// </summary>
    [YamlMember(Alias = "files")]
    //public List<string> Files { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> Files { get; set; } = new();
    
    /// <summary>
    /// List of files modified by this preset
    /// </summary>
    [YamlMember(Alias = "additional_files")]
    public List<string> AdditionalFiles { get; set; } = new();
}