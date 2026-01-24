using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Companion.Events;
using Companion.Models;
using Companion.Models.Presets;
using Serilog;

namespace Companion.Services.Presets;

/// <summary>
/// Service for applying and managing presets
/// </summary>
[Obsolete("Deprecated: PresetService is legacy and will be replaced by a newer preset workflow.")]
public class PresetService : IPresetService
{
    private readonly ILogger _logger;
    private readonly ISshClientService _sshClientService;
    private readonly IEventSubscriptionService _eventSubscriptionService;
    private readonly IYamlConfigService _yamlConfigService;
    private readonly IGlobalSettingsService _globalSettingsService;
    private readonly Dictionary<string, string> _cachedFileContents = new();

    // Add a field to track the active preset being applied
    private Preset? _activePreset;
    private readonly string _presetsFolder = Path.Combine(Path.GetTempPath(), "OpenIPC Companion", "Presets");

    // List of critical system files to ignore
    private readonly HashSet<string> _criticalSystemFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "passwd",
        "shadow",
        "group",
        "fstab",
        "network/interfaces",
        "hostname",
        "alink.conf"
        // Add any other critical system files here
    };

    /// <summary>
    /// Initializes a new instance of PresetService
    /// </summary>
    public PresetService(
        ILogger logger,
        ISshClientService sshClientService,
        IEventSubscriptionService eventSubscriptionService,
        IYamlConfigService yamlConfigService,
        IGlobalSettingsService globalSettingsService)
    {
        _logger = logger?.ForContext(GetType()) ?? 
                 throw new ArgumentNullException(nameof(logger));
        _sshClientService = sshClientService ?? throw new ArgumentNullException(nameof(sshClientService));
        _eventSubscriptionService = eventSubscriptionService ??
                                    throw new ArgumentNullException(nameof(eventSubscriptionService));
        _yamlConfigService = yamlConfigService ?? throw new ArgumentNullException(nameof(yamlConfigService));
        _globalSettingsService = globalSettingsService;
        Directory.CreateDirectory(_presetsFolder);
    }

    /// <inheritdoc />
    public async Task<bool> ApplyPresetAsync(Preset preset)
    {
        try
        {
            _logger.Information($"Applying preset: {preset.Name}");

            // Set the active preset for the duration of this operation
            _activePreset = preset;

            // Load the preset files if not already loaded
            if (preset.FileModifications.Count == 0)
            {
                bool loaded = await LoadPresetFilesAsync(preset);
                if (!loaded)
                {
                    _logger.Error($"Failed to load files for preset: {preset.Name}");
                    _activePreset = null; // Reset active preset
                    return false;
                }
            }

            bool success = true;

            // First, handle any additional files that need to be copied directly
            if (preset.AdditionalFiles != null && preset.AdditionalFiles.Count > 0)
            {
                _logger.Information($"Processing {preset.AdditionalFiles.Count} additional files");

                foreach (var fileName in preset.AdditionalFiles)
                {
                    try
                    {
                        _logger.Information($"Copying additional file: {fileName}");

                        // Check if the file is a critical system file
                        if (IsCriticalSystemFile(fileName))
                        {
                            _logger.Warning($"Skipping critical system file: {fileName}");
                            continue;
                        }
                        
                        // Get the destination path on the device
                        string filePath = GetFilePathForName(fileName);

                        // Copy the file directly from preset folder to device
                        string fileContent = await CopyFileFromPresetsDirectlyAsync(fileName);

                        if (string.IsNullOrEmpty(fileContent))
                        {
                            _logger.Error($"Failed to copy additional file: {fileName}");
                            success = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error processing additional file {fileName}: {ex.Message}");
                        success = false;
                    }
                }
            }

            // Process each file modification in the preset
            foreach (var fileModification in preset.FileModifications)
            {
                string fileName = fileModification.FileName;
                string filePath;
                string fileContent;
                var changes = fileModification.Changes;
                bool wfbYamlEnabled = _globalSettingsService.IsWfbYamlEnabled;

                _logger.Information($"Applying changes to file: {fileName}");

                if ((wfbYamlEnabled && fileName == "wfb.conf") || (wfbYamlEnabled && fileName == "telemetry.conf"))
                    return true;
                
                // Special handling for known binary/special files that should be copied directly
                if (ShouldCopyFileDirectly(fileName))
                {
                    _logger.Information($"File {fileName} will be copied directly from preset folder");

                    // Get the destination path
                    filePath = GetFilePathForName(fileName);

                    // Copy the file directly
                    fileContent = await CopyFileFromPresetsDirectlyAsync(fileName);

                    if (string.IsNullOrEmpty(fileContent))
                    {
                        _logger.Error($"Failed to copy file: {fileName}");
                        success = false;
                    }

                    continue; // Skip the normal modification process
                }

                // Get the current file content from the device
                filePath = GetFilePathForName(fileName);
                fileContent = await FetchFileContentAsync(filePath);

                if (string.IsNullOrEmpty(fileContent))
                {
                    _logger.Error($"Failed to fetch file content for {fileName}");
                    success = false;
                    continue;
                }

                // Apply changes based on file type
                string updatedContent = await ApplyChangesToFileAsync(fileName, fileContent, changes);

                if (string.IsNullOrEmpty(updatedContent))
                {
                    _logger.Error($"Failed to apply changes to {fileName}");
                    success = false;
                    continue;
                }

                // Upload the updated file back to the device
                bool uploadSuccess = await UploadFileContentAsync(filePath, updatedContent);

                if (!uploadSuccess)
                {
                    _logger.Error($"Failed to upload updated content for {fileName}");
                    success = false;
                    continue;
                }
            }

            // Restart services after applying all changes
            if (success)
            {
                await RestartServicesAsync(preset);

                // Trigger UI refresh by publishing events
                await RefreshUIAsync();
            }

            // Reset active preset
            _activePreset = null;

            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error applying preset: {ex.Message}");
            _activePreset = null; // Reset active preset on error
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> LoadPresetFilesAsync(Preset preset)
    {
        try
        {
            _logger.Information($"Loading files for preset: {preset.Name}");

            // Clear existing file modifications
            preset.FileModifications.Clear();

            // Get the preset folder path
            string presetFolderPath = preset.FolderPath;
            if (string.IsNullOrEmpty(presetFolderPath))
            {
                _logger.Error($"Preset folder path is empty for preset: {preset.Name}");
                return false;
            }

            _logger.Information($"Preset folder path: {presetFolderPath}");

            // Check if the preset folder exists
            if (!Directory.Exists(presetFolderPath))
            {
                _logger.Error($"Preset folder not found: {presetFolderPath}");
                return false;
            }

            // Process each file listed in preset.Files
            foreach (var fileEntry in preset.Files)
            {
                string fileName = fileEntry.Key;
                var fileChanges = fileEntry.Value;

                _logger.Information($"Loading file: {fileName} with {fileChanges.Count} changes");

                // Create file modification
                var fileModification = new FileModification
                {
                    FileName = fileName,
                    Changes = new ObservableCollection<KeyValuePair<string, string>>(fileChanges)
                };

                preset.FileModifications.Add(fileModification);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading preset files: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Preset> CreatePresetFromCurrentConfigAsync(string name, string category, string description)
    {
        try
        {
            _logger.Information($"Creating preset from current configuration: {name}");

            // Create a new preset
            var preset = new Preset
            {
                Name = name,
                Category = category,
                Description = description,
                Author = Environment.UserName,
                Status = "Draft",
                State = "Community"
            };

            // Fetch current configurations
            string wfbConfContent = await FetchFileContentAsync(OpenIPC.WfbConfFileLoc);
            string wfbYamlContent = await FetchFileContentAsync(OpenIPC.WfbYamlFileLoc);
            string majesticYamlContent = await FetchFileContentAsync(OpenIPC.MajesticFileLoc);

            var files = new Dictionary<string, Dictionary<string, string>>();

            // Create file modifications for wfb.conf
            if (!string.IsNullOrEmpty(wfbConfContent))
            {
                var wfbParser = new WfbConfigParser();
                wfbParser.ParseConfigString(wfbConfContent);

                var wfbChanges = new Dictionary<string, string>
                {
                    { "channel", wfbParser.Channel },
                    { "txpower", wfbParser.TxPower.ToString() },
                    { "driver_txpower_override", wfbParser.DriverTxPowerOverride.ToString() },
                    { "bandwidth", wfbParser.Bandwidth.ToString() },
                    { "stbc", wfbParser.Stbc.ToString() },
                    { "ldpc", wfbParser.Ldpc.ToString() },
                    { "mcs_index", wfbParser.McsIndex.ToString() },
                    { "fec_k", wfbParser.FecK.ToString() },
                    { "fec_n", wfbParser.FecN.ToString() }
                };

                files["wfb.conf"] = wfbChanges;
            }

            // Create file modifications for majestic.yaml
            if (!string.IsNullOrEmpty(majesticYamlContent))
            {
                var majesticConfig = new Dictionary<string, string>();
                _yamlConfigService.ParseYaml(majesticYamlContent, majesticConfig);

                var majesticChanges = new Dictionary<string, string>();

                // Add only the most important settings
                AddIfExists(majesticConfig, majesticChanges, Majestic.VideoSize);
                AddIfExists(majesticConfig, majesticChanges, Majestic.VideoFps);
                AddIfExists(majesticConfig, majesticChanges, Majestic.VideoCodec);
                AddIfExists(majesticConfig, majesticChanges, Majestic.VideoBitrate);
                AddIfExists(majesticConfig, majesticChanges, Majestic.ImageFlip);
                AddIfExists(majesticConfig, majesticChanges, Majestic.ImageMirror);

                // Add FPV settings if enabled
                if (majesticConfig.TryGetValue(Majestic.FpvEnabled, out var fpvEnabled) && fpvEnabled == "true")
                {
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvEnabled);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvNoiseLevel);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvRoiQp);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvRoiRect);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvRefEnhance);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvRefPred);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvIntraLine);
                    AddIfExists(majesticConfig, majesticChanges, Majestic.FpvIntraQp);
                }

                files["majestic.yaml"] = majesticChanges;
            }

            // Set the Files dictionary in the preset
            preset.Files = files;

            // Initialize FileModifications from Files
            preset.InitializeFileModifications();

            return preset;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error creating preset from current configuration: {ex.Message}");
            return null;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Checks if the given filename is a critical system file that should be ignored.
    /// </summary>
    /// <param name="fileName">The filename to check.</param>
    /// <returns>True if the file is a critical system file; otherwise, false.</returns>
    private bool IsCriticalSystemFile(string fileName)
    {
        // Remove leading "/etc/" if present for simpler matching
        if (fileName.StartsWith("/etc/", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName.Substring(5);  // Remove "/etc/"
        }
        return _criticalSystemFiles.Contains(fileName);
    }
    
    /// <summary>
    /// Maps a file name to its path on the device
    /// </summary>
    private string GetFilePathForName(string fileName)
    {
        return fileName.ToLower() switch
        {
            "wfb.conf" => OpenIPC.WfbConfFileLoc,
            "wfb.yaml" => OpenIPC.WfbYamlFileLoc,
            "majestic.yaml" => OpenIPC.MajesticFileLoc,
            _ => $"/etc/{fileName}" // Default location for other files
        };
    }

    /// <summary>
    /// Fetches file content from the device
    /// </summary>
    private async Task<string> FetchFileContentAsync(string filePath)
    {
        try
        {
            // Check if we already have the content cached
            if (_cachedFileContents.TryGetValue(filePath, out var cachedContent))
            {
                return cachedContent;
            }

            // Fetch content from device
            var content = await _sshClientService.DownloadFileAsync(DeviceConfig.Instance, filePath);

            // Cache the content
            if (!string.IsNullOrEmpty(content))
            {
                _cachedFileContents[filePath] = content;
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching file content for {filePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies changes to a file based on its type
    /// </summary>
    private async Task<string> ApplyChangesToFileAsync(string fileName, string fileContent,
        IEnumerable<KeyValuePair<string, string>> changes)
    {
        try
        {
            bool wfbYamlEnabled = _globalSettingsService.IsWfbYamlEnabled;
            switch (fileName.ToLower())
            {
                case "wfb.conf":
                    if (wfbYamlEnabled)
                        return "";
                    
                    return ApplyChangesToWfbConf(fileContent, changes);

                case "telemetry.conf":
                    if (wfbYamlEnabled)
                        return "";
                    
                    return ApplyChangesToWfbConf(fileContent, changes);

                case "wfb.yaml":
                    return ApplyChangesToYaml(fileContent, changes);

                case "majestic.yaml":
                    return ApplyChangesToYaml(fileContent, changes);

                case "vtxmenu.ini":
                    // For vtxmenu.ini, we want to directly copy the preset file to the drone
                    return await CopyFileFromPresetsDirectlyAsync(fileName);

                default:
                    _logger.Warning($"Unsupported file type: {fileName}");
                    return fileContent;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error applying changes to {fileName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets the folder path for the active preset
    /// </summary>
    private string GetActivePresetFolder()
    {
        // Check if we're applying a preset and use its folder
        if (_activePreset != null && !string.IsNullOrEmpty(_activePreset.FolderPath))
        {
            return _activePreset.FolderPath;
        }

        // Otherwise return the default presets folder
        return _presetsFolder;
    }

    /// <summary>
    /// Determines if a file should be copied directly instead of being modified
    /// </summary>
    private bool ShouldCopyFileDirectly(string fileName)
    {
        // Skip standard configuration files that should be modified rather than copied
        var configFiles = new[]
        {
            "wfb.conf",
            "telemetry.conf",
            "majestic.yaml",
            "wfb.yaml"
        };

        if (configFiles.Contains(fileName.ToLowerInvariant()))
        {
            return false;
        }

        // List of file extensions or names that should be copied directly
        var directCopyExtensions = new[] { ".ini", ".bin", ".fw", ".img", ".dat" };
        var directCopyFiles = new[] { "vtxmenu.ini" };

        // Check if the file has a direct copy extension or is a known direct copy file
        return directCopyFiles.Contains(fileName.ToLowerInvariant()) ||
               directCopyExtensions.Any(ext => fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Determines if a file is a binary file based on extension
    /// </summary>
    private bool IsBinaryFile(string fileName)
    {
        // List of extensions for binary files
        var binaryExtensions = new[] { ".bin", ".fw", ".img", ".dat" };

        // Check the extension
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return binaryExtensions.Contains(extension);
    }

    /// <summary>
    /// Copies a file directly from the presets folder to the device instead of modifying its content
    /// </summary>
    private async Task<string> CopyFileFromPresetsDirectlyAsync(string fileName)
    {
        try
        {
            // Construct the full path to the source file in the active preset folder
            string presetFolderPath = _activePreset?.FolderPath;

            if (string.IsNullOrEmpty(presetFolderPath))
            {
                _logger.Error("No active preset folder path available");
                return null;
            }

            string sourceFilePath = Path.Combine(presetFolderPath, fileName);

            _logger.Information($"Looking for source file at: {sourceFilePath}");

            if (!File.Exists(sourceFilePath))
            {
                _logger.Error($"Source file not found: {sourceFilePath}");
                return null;
            }

            // Get the destination path on the device
            string destFilePath = GetFilePathForName(fileName);

            _logger.Information($"Will copy to device at: {destFilePath}");

            // Check if it's a text or binary file
            bool isBinary = IsBinaryFile(fileName);

            if (isBinary)
            {
                // Read and upload binary file
                byte[] fileBytes = await File.ReadAllBytesAsync(sourceFilePath);
                await _sshClientService.UploadFileAsync(DeviceConfig.Instance, sourceFilePath, destFilePath);
                _logger.Information($"Copied binary file {fileName} from preset folder to {destFilePath}");
                return "BINARY_CONTENT"; // Return a placeholder for binary content
            }
            else
            {
                // Read and upload text file
                string fileContent = await File.ReadAllTextAsync(sourceFilePath);
                await _sshClientService.UploadFileStringAsync(DeviceConfig.Instance, destFilePath, fileContent);
                _logger.Information($"Copied text file {fileName} from preset folder to {destFilePath}");
                return fileContent;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error copying file from presets folder: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Applies changes to wfb.conf file
    /// </summary>
    private string ApplyChangesToWfbConf(string fileContent, IEnumerable<KeyValuePair<string, string>> changes)
    {
        var regex = new Regex(@"^(?!#.*)([\w_]+)=.*", RegexOptions.Multiline);
        var result = fileContent;

        foreach (var change in changes)
        {
            string pattern = $"^(?!#.*)({Regex.Escape(change.Key)})=.*";
            string replacement = $"{change.Key}={change.Value}";

            if (Regex.IsMatch(result, pattern, RegexOptions.Multiline))
            {
                // Key exists, update it
                result = Regex.Replace(result, pattern, replacement, RegexOptions.Multiline);
                _logger.Debug($"Updated {change.Key}={change.Value} in wfb.conf");
            }
            else
            {
                // Key doesn't exist, add it
                result += $"\n{replacement}";
                _logger.Debug($"Added {change.Key}={change.Value} to wfb.conf");
            }
        }

        return result;
    }

    /// <summary>
    /// Applies changes to majestic.yaml and wfb.yaml file
    /// </summary>
    private string ApplyChangesToYaml(string fileContent, IEnumerable<KeyValuePair<string, string>> changes)
    {
        // Parse the existing YAML content
        var yamlConfig = new Dictionary<string, string>();
        _yamlConfigService.ParseYaml(fileContent, yamlConfig);

        // Apply changes to the YAML configuration
        foreach (var change in changes)
        {
            yamlConfig[change.Key] = change.Value;
            _logger.Debug($"Set {change.Key}={change.Value} in majestic.yaml");
        }

        // Generate updated YAML content
        return _yamlConfigService.UpdateYaml(yamlConfig);
    }


    /// <summary>
    /// Uploads file content to the device
    /// </summary>
    private async Task<bool> UploadFileContentAsync(string filePath, string content)
    {
        try
        {
            // Update the cache with the new content
            _cachedFileContents[filePath] = content;

            // Upload to device
            await _sshClientService.UploadFileStringAsync(DeviceConfig.Instance, filePath, content);
            _logger.Information($"Uploaded updated content to {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error uploading file content to {filePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restarts services affected by the preset changes
    /// </summary>
    private async Task RestartServicesAsync(Preset preset)
    {
        try
        {
            bool restartWfb = false;
            bool restartMajestic = false;

            // Check which services need to be restarted
            foreach (var fileModification in preset.FileModifications)
            {
                if (fileModification.FileName.ToLower() == "wfb.conf")
                {
                    restartWfb = true;
                }
                else if (fileModification.FileName.ToLower() == "majestic.yaml")
                {
                    restartMajestic = true;
                }
            }

            // Restart WFB if needed
            if (restartWfb)
            {
                _logger.Information("Restarting WFB service");
                await _sshClientService.ExecuteCommandAsync(DeviceConfig.Instance, DeviceCommands.WfbRestartCommand);
            }

            // Restart Majestic if needed
            if (restartMajestic)
            {
                _logger.Information("Restarting Majestic service");
                await _sshClientService.ExecuteCommandAsync(DeviceConfig.Instance,
                    DeviceCommands.MajesticRestartCommand);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error restarting services: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Refreshes the UI by publishing updated content events
    /// </summary>
    private async Task RefreshUIAsync()
    {
        try
        {
            // Clear the file cache to force refresh
            _cachedFileContents.Clear();

            // Re-fetch wfb.conf and publish update event
            string wfbConfContent = await FetchFileContentAsync(OpenIPC.WfbConfFileLoc);
            if (!string.IsNullOrEmpty(wfbConfContent))
            {
                _eventSubscriptionService.Publish<WfbConfContentUpdatedEvent, WfbConfContentUpdatedMessage>(
                    new WfbConfContentUpdatedMessage(wfbConfContent));
            }

            // string wfbYamlContent = await FetchFileContentAsync(OpenIPC.WfbYamlFileLoc);
            // if (!string.IsNullOrEmpty(wfbYamlContent))
            // {
            //     _eventSubscriptionService.Publish<WfbYamlContentUpdatedEvent, WfbYamlContentUpdatedMessage>(
            //         new WfbYamlContentUpdatedMessage(wfbConfContent));
            // }

            // Re-fetch majestic.yaml and publish update event
            string majesticYamlContent = await FetchFileContentAsync(OpenIPC.MajesticFileLoc);
            if (!string.IsNullOrEmpty(majesticYamlContent))
            {
                _eventSubscriptionService.Publish<MajesticContentUpdatedEvent, MajesticContentUpdatedMessage>(
                    new MajesticContentUpdatedMessage(majesticYamlContent));
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error refreshing UI: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Helper method to add a value from source dictionary to target dictionary if it exists
    /// </summary>
    private void AddIfExists(Dictionary<string, string> source, Dictionary<string, string> target, string key)
    {
        if (source.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
        {
            target[key] = value;
        }
    }

    #endregion
}
