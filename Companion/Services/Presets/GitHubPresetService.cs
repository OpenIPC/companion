using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Companion.Models.Presets;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Companion.Services.Presets;

/// <summary>
/// Service for fetching and managing presets from GitHub repositories
/// </summary>
public class GitHubPresetService : IGitHubPresetService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private const string GithubApiBaseUrl = "https://api.github.com";

    public GitHubPresetService(ILogger logger, HttpClient? httpClient = null)
    {
        _logger = logger?.ForContext(GetType()) ?? 
                 throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OpenIPC Companion");
    }

    /// <summary>
    /// Gets a platform-independent temporary directory path
    /// </summary>
    private string GetTempDirectory()
    {
        // Use the system's temp directory as a base
        string baseDir = Path.Combine(Path.GetTempPath(), "OpenIPC Companion", "Presets");
    
        // Create the directory if it doesn't exist
        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }
    
        return baseDir;
    }

    /// <summary>
    /// Sanitizes repository name to create a valid directory name
    /// </summary>
    /// <summary>
    /// Sanitizes repository name to create a valid directory name
    /// </summary>
    private string SanitizeRepositoryName(Repository repository)
    {
        // Get the repository URL and extract owner and repo name
        string repoUrl = repository.Url.TrimEnd('/');
    
        // Extract the owner/repo part from the URL
        string ownerRepo = repoUrl.Split('/').Skip(3).Take(2).Aggregate((a, b) => a + "/" + b);
    
        // If we successfully extracted owner/repo, format it as owner-repo
        if (ownerRepo.Contains("/"))
        {
            string[] parts = ownerRepo.Split('/');
            string owner = parts[0];
            string repo = parts[1];
        
            // Replace owner/repo with owner-repo format
            repoUrl = $"{owner}-{repo}";
        }
        else
        {
            // Fallback to repository name if we couldn't extract owner/repo
            repoUrl = repository.RepositoryName;
        }
    
        // Remove any characters that aren't suitable for directory names
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            repoUrl = repoUrl.Replace(c, '-');
        }
    
        return repoUrl;
    }
    
    /// <summary>
    /// Fetches preset files from a GitHub repository
    /// </summary>
    /// <param name="repository">Repository to fetch presets from</param>
    /// <returns>List of GitHub files containing preset configurations</returns>
    public async Task<List<GitHubFile>> FetchPresetFilesAsync(Repository repository)
    {
        try
        {
            // Fetch PRESET_INDEX.yaml directly from raw GitHub content
            var indexUrl = repository.GetPresetIndexUrl();
            _logger.Information($"Fetching preset index from: {indexUrl}");

            var response = await _httpClient.GetStringAsync(indexUrl);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var presetIndex = deserializer.Deserialize<PresetIndex>(response);

            // Convert index entries to GitHubFiles
            return presetIndex.Presets.Select(preset => new GitHubFile
            {
                Name = preset.Name,
                Path = preset.Path,
                DownloadUrl = repository.GetPresetDownloadUrl(preset.Path)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching preset index: {ex.Message}");
            return new List<GitHubFile>();
        }
    }

    /// <summary>
    /// Downloads a preset configuration file and any additional files it references
    /// </summary>
    /// <param name="githubFile">GitHub file to download</param>
    /// <param name="localBaseDirectory">Base directory to save the preset</param>
    /// <returns>Local path of the downloaded preset configuration</returns>
    public async Task<string?> DownloadPresetConfigAsync(GitHubFile githubFile, string localBaseDirectory, string presetName)
    {
        try
        {
            _logger.Information($"Downloading preset from: {githubFile.DownloadUrl}");
        
            // Download file content
            var fileContent = await _httpClient.GetStringAsync(githubFile.DownloadUrl);

            // Create directory using sanitized preset name
            var localPresetDir = Path.Combine(localBaseDirectory, presetName);
        
            _logger.Information($"Creating directory: {localPresetDir}");
            Directory.CreateDirectory(localPresetDir);

            // Save the preset configuration
            var localConfigPath = Path.Combine(localPresetDir, "preset-config.yaml");
            await File.WriteAllTextAsync(localConfigPath, fileContent);
        
            _logger.Information($"Saved preset config to: {localConfigPath}");
        
            // Parse the preset config to check for additional files
            await DownloadAdditionalFilesAsync(fileContent, localPresetDir, githubFile);

            return localConfigPath;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error downloading preset {githubFile.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Downloads any additional files referenced in the preset configuration
    /// </summary>
    /// <param name="presetConfigYaml">The YAML content of the preset configuration</param>
    /// <param name="localPresetDir">Local directory where the preset is saved</param>
    /// <param name="githubFile">The original GitHub file information</param>
    /// <summary>
    /// Downloads any additional files referenced in the preset configuration
    /// </summary>
    private async Task DownloadAdditionalFilesAsync(string presetConfigYaml, string localPresetDir,
        GitHubFile githubFile)
    {
        try
        {
            // Parse the YAML content
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            // Parse as dictionary first to avoid Preset model dependency issues
            var presetConfig = deserializer.Deserialize<Dictionary<string, object>>(presetConfigYaml);

            // ONLY look for the additional_files section - DON'T process the "files" section
            if (presetConfig.TryGetValue("additional_files", out var additionalFilesObj) &&
                additionalFilesObj is List<object> additionalFiles)
            {
                _logger.Information($"Found {additionalFiles.Count} additional files to download");

                // Extract the base URL for the repository folder
                string baseRepoUrl = GetBaseRepoUrl(githubFile.DownloadUrl);

                // Download each additional file
                foreach (var fileObj in additionalFiles)
                {
                    if (fileObj is string fileName)
                    {
                        await DownloadAdditionalFileAsync(fileName, baseRepoUrl, localPresetDir);
                    }
                }
            }
            else
            {
                _logger.Information("No additional_files section found in preset configuration");
            }

            // DO NOT process the "files" section for downloading
        }
        catch (Exception ex)
        {
            _logger.Error($"Error processing additional files: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads a single additional file
    /// </summary>
    /// <param name="fileName">Name of the file to download</param>
    /// <param name="baseRepoUrl">Base URL of the repository folder</param>
    /// <param name="localPresetDir">Local directory to save the file</param>
    /// <param name="customUrl">Optional custom URL for the file</param>
    private async Task DownloadAdditionalFileAsync(string fileName, string baseRepoUrl, string localPresetDir,
        string customUrl = null)
    {
        try
        {
            // Construct the download URL
            string downloadUrl = customUrl ?? $"{baseRepoUrl}/{fileName}";

            _logger.Information($"Downloading additional file: {fileName} from {downloadUrl}");

            try
            {
                // Download the file content
                var fileContent = await _httpClient.GetStringAsync(downloadUrl);

                // Save to the preset directory
                var localFilePath = Path.Combine(localPresetDir, fileName);
                await File.WriteAllTextAsync(localFilePath, fileContent);

                _logger.Information($"Saved additional file to: {localFilePath}");
            }
            catch (HttpRequestException ex)
            {
                // Handle binary files differently
                if (IsBinaryFile(fileName))
                {
                    await DownloadBinaryFileAsync(downloadUrl, fileName, localPresetDir);
                }
                else
                {
                    // Re-throw if it's not a binary file issue
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error downloading file {fileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Downloads a binary file from a URL
    /// </summary>
    /// <param name="url">URL of the binary file</param>
    /// <param name="fileName">Name to save the file as</param>
    /// <param name="localPresetDir">Directory to save the file in</param>
    private async Task DownloadBinaryFileAsync(string url, string fileName, string localPresetDir)
    {
        try
        {
            _logger.Information($"Downloading binary file: {fileName} from {url}");

            // Get binary content
            var response = await _httpClient.GetByteArrayAsync(url);

            // Save binary data
            var localFilePath = Path.Combine(localPresetDir, fileName);
            await File.WriteAllBytesAsync(localFilePath, response);

            _logger.Information($"Saved binary file to: {localFilePath}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error downloading binary file {fileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if a file is likely a binary file based on extension
    /// </summary>
    /// <param name="fileName">Name of the file to check</param>
    /// <returns>True if the file is likely binary</returns>
    private bool IsBinaryFile(string fileName)
    {
        // Add extensions of binary files you might encounter
        var binaryExtensions = new[] { ".bin", ".ini", ".dat", ".img", ".fw" };
        string extension = Path.GetExtension(fileName).ToLowerInvariant();

        return binaryExtensions.Contains(extension);
    }

    /// <summary>
    /// Extracts the base repository URL from a file download URL
    /// </summary>
    /// <param name="fileUrl">The URL of a file in the repository</param>
    /// <returns>The base URL for the containing folder</returns>
    private string GetBaseRepoUrl(string fileUrl)
    {
        // Remove the filename from the URL to get the base folder URL
        int lastSlashIndex = fileUrl.LastIndexOf('/');
        if (lastSlashIndex > 0)
        {
            return fileUrl.Substring(0, lastSlashIndex);
        }

        return fileUrl;
    }

    /// <summary>
    /// Synchronizes presets from a repository
    /// </summary>
    /// <param name="repository">Repository to sync</param>
    /// <param name="localPresetsDirectory">Local directory to save presets</param>
    /// <returns>List of downloaded preset configurations</returns>
    /// <summary>

public async Task<List<string>> SyncRepositoryPresetsAsync(
    Repository repository,
    string localPresetsDirectory)
{
    var downloadedPresets = new List<string>();

    try
    {
        _logger.Information($"Syncing presets from repository: {repository.Name}");
        
        // Get a clean repository name for the directory
        string sanitizedRepoName = SanitizeRepositoryName(repository);
        
        // Use temp directory if supplied directory is null or empty
        if (string.IsNullOrWhiteSpace(localPresetsDirectory))
        {
            // Create a repository-specific directory with sanitized name
            localPresetsDirectory = Path.Combine(GetTempDirectory(), sanitizedRepoName);
        }
        else
        {
            // If a custom directory is provided, still ensure repository isolation with sanitized name
            localPresetsDirectory = Path.Combine(localPresetsDirectory, sanitizedRepoName);
        }
        
        _logger.Information($"Using directory for repository: {localPresetsDirectory}");
        
        // Create the base directory if it doesn't exist
        if (!Directory.Exists(localPresetsDirectory))
        {
            Directory.CreateDirectory(localPresetsDirectory);
        }
        else 
        {
            // Clear existing repository directory to avoid stale data
            _logger.Information($"Cleaning existing repository directory: {localPresetsDirectory}");
            
            // Delete all files but preserve the directory
            foreach (string file in Directory.GetFiles(localPresetsDirectory, "*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
            
            foreach (string dir in Directory.GetDirectories(localPresetsDirectory))
            {
                Directory.Delete(dir, true);
            }
        }
        
        // Fetch preset files
        var presetFiles = await FetchPresetFilesAsync(repository);
        _logger.Information($"Found {presetFiles.Count} presets in index");

        // Download each preset configuration
        foreach (var presetFile in presetFiles)
        {
            string presetName = presetFile.Name;
            // Sanitize preset name to ensure it's a valid directory name
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                presetName = presetName.Replace(c, '-');
            }
            
            var localConfigPath = await DownloadPresetConfigAsync(
                presetFile,
                localPresetsDirectory,
                presetName
            );

            if (localConfigPath != null)
                downloadedPresets.Add(localConfigPath);
        }

        _logger.Information($"Successfully downloaded {downloadedPresets.Count} presets from {repository.Name}");
    }
    catch (Exception ex)
    {
        _logger.Error($"Error syncing repository {repository.Name}: {ex.Message}");
    }

    return downloadedPresets;
}
}