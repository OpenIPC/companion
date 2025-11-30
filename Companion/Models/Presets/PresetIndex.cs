using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;


namespace Companion.Models.Presets;

/// <summary>
/// Represents the structure of the PRESET_INDEX.yaml file
/// </summary>
public class PresetIndex
{
    /// <summary>
    /// Version of the preset index format
    /// </summary>
    [YamlMember(Alias = "version")]
    public string Version { get; set; }

    /// <summary>
    /// Timestamp of when the index was last updated
    /// </summary>
    [YamlMember(Alias = "last_updated")]
    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Total number of presets in the index
    /// </summary>
    [YamlMember(Alias = "total_presets")]
    public int TotalPresets { get; set; }

    /// <summary>
    /// Collection of preset entries
    /// </summary>
    [YamlMember(Alias = "presets")]
    public List<PresetIndexEntry> Presets { get; set; } = new();
}

