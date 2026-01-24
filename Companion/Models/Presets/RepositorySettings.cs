namespace Companion.Models.Presets;

/// <summary>
/// Settings for a preset repository from configuration
/// </summary>
public class RepositorySettings
{
    /// <summary>
    /// The full URL of the repository
    /// </summary>
    public string Url { get; set; } = string.Empty;
        
    /// <summary>
    /// The branch to use for fetching presets
    /// </summary>
    public string Branch { get; set; } = string.Empty;
        
    /// <summary>
    /// Optional description of the repository
    /// </summary>
    public string Description { get; set; } = string.Empty;
        
    /// <summary>
    /// Indicates whether the repository is active
    /// </summary>
    public bool IsActive { get; set; } = true;
}
