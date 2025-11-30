using System.Threading.Tasks;
using Companion.Models.Presets;

namespace Companion.Services.Presets;

/// <summary>
/// Interface for Preset Service to apply and manage presets
/// </summary>
public interface IPresetService
{
    /// <summary>
    /// Apply a preset to the device
    /// </summary>
    /// <param name="preset">The preset to apply</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task<bool> ApplyPresetAsync(Preset preset);
    
    /// <summary>
    /// Create a preset from current device configuration
    /// </summary>
    /// <param name="name">Name for the new preset</param>
    /// <param name="category">Category for the new preset</param>
    /// <param name="description">Description for the new preset</param>
    /// <returns>The newly created preset</returns>
    Task<Preset> CreatePresetFromCurrentConfigAsync(string name, string category, string description);
    
    /// <summary>
    /// Load the content of preset files from disk
    /// </summary>
    /// <param name="preset">The preset to load files for</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task<bool> LoadPresetFilesAsync(Preset preset);
}