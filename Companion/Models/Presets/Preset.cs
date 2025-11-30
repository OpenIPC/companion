using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Companion.Models.Presets;

public class Preset
{
    /* Name of preset */
    public string Name { get; set; } = string.Empty;
    
    /* Path to the preset relative to repository root */
    public string PresetPath { get; set; } = string.Empty;
    
    public string Category { get; set; } = string.Empty;
    public ObservableCollection<string> Tags { get; set; } = new();
    public string Author { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObservableCollection<FileModification> FileModifications { get; set; } = new();
    
    [YamlMember(Alias = "additional_files")] 
    public List<string> AdditionalFiles { get; set; } = new();
    
    /* States can be used for Official, Community, Untested, etc. */
    public string State { get; set; } = string.Empty;
    public string? Sensor { get; set; }
    
    /* Dictionary of files with their changes */
    public Dictionary<string, Dictionary<string, string>> Files { get; set; } = new();

    /* Local path where the preset config is stored */
    [YamlIgnore] 
    public string FolderPath { get; set; } = string.Empty;

    /* Summary of file modifications for display */
    public string FileModificationsSummary => GetFileModificationsSummary();

    /// <summary>
    /// Load a Preset object from a YAML file.
    /// </summary>
    /// <param name="configPath">Path to the preset-config.yaml file.</param>
    /// <returns>Loaded Preset object.</returns>
    public static Preset LoadFromFile(string configPath)
    {
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException($"Preset configuration file not found: {configPath}");
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance) // Change to underscore convention
            .Build();

        var yamlContent = File.ReadAllText(configPath);
        var preset = deserializer.Deserialize<Preset>(yamlContent);
    
        // Set the folder path to the directory containing the config file
        preset.FolderPath = System.IO.Path.GetDirectoryName(configPath);
    
        // Initialize FileModifications from Files dictionary
        preset.InitializeFileModifications();
    
        return preset;
    }

    /// <summary>
    /// Convert Files dictionary into a bindable collection of FileModifications.
    /// </summary>
    public void InitializeFileModifications()
    {
        FileModifications.Clear();
        foreach (var file in Files)
        {
            FileModifications.Add(new FileModification
            {
                FileName = file.Key,
                Changes = new ObservableCollection<KeyValuePair<string, string>>(file.Value)
            });
        }
    }
    
    /// <summary>
    /// Generates a human-readable summary of all file modifications
    /// </summary>
    private string GetFileModificationsSummary()
    {
        var summary = new StringBuilder();
        
        // Ensure FileModifications is initialized
        if (FileModifications.Count == 0 && Files.Count > 0)
        {
            InitializeFileModifications();
        }
        
        foreach (var fileModification in FileModifications)
        {
            if (summary.Length > 0)
            {
                summary.Append(", ");
            }
            
            summary.Append($"{fileModification.FileName}: ");
            
            var changes = new List<string>();
            foreach (var change in fileModification.Changes.Take(3)) // Limit to first 3 changes
            {
                changes.Add($"{change.Key} = {change.Value}");
            }
            
            if (fileModification.Changes.Count > 3)
            {
                changes.Add($"...(+{fileModification.Changes.Count - 3} more)");
            }
            
            summary.Append(string.Join(", ", changes));
        }
        
        return summary.ToString();
    }
    
    /// <summary>
    /// Save the preset to a YAML file
    /// </summary>
    public void SaveToFile(string filePath)
    {
        // Sync FileModifications to Files before saving
        SyncFileModificationsToFiles();
        
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        
        var yaml = serializer.Serialize(this);
        File.WriteAllText(filePath, yaml);
        
        // Update the folder path
        FolderPath = System.IO.Path.GetDirectoryName(filePath);
    }
    
    /// <summary>
    /// Synchronize changes from FileModifications to Files dictionary
    /// </summary>
    private void SyncFileModificationsToFiles()
    {
        Files.Clear();
        
        foreach (var modification in FileModifications)
        {
            var changes = new Dictionary<string, string>();
            
            foreach (var change in modification.Changes)
            {
                changes[change.Key] = change.Value;
            }
            
            Files[modification.FileName] = changes;
        }
    }
}