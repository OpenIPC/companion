using System;
using System.IO;
using Newtonsoft.Json;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public static class SettingsManager
{
    private static readonly string AppSettingsName = "openipc_settings.json";

    public static string AppSettingFilename
    {
        get;
        set;
        // Allow setting a custom filename for testing
    } = $"{OpenIPC.AppDataConfigDirectory}/openipc_settings.json";


    /// <summary>
    ///     Loads the device configuration settings from a JSON file.
    /// </summary>
    /// <returns>
    ///     A <see cref="DeviceConfig" /> object containing the loaded settings.
    ///     If the settings file does not exist, returns a <see cref="DeviceConfig" />
    ///     with default values.
    /// </returns>
    public static DeviceConfig? LoadSettings()
    {
        DeviceConfig deviceConfig;

        if (File.Exists(AppSettingFilename))
            try
            {
                var json = File.ReadAllText(AppSettingFilename);
                deviceConfig = JsonConvert.DeserializeObject<DeviceConfig>(json);

                if (deviceConfig != null)
                    // Optionally publish an event if needed
                    // eventAggregator?.GetEvent<DeviceStateUpdatedEvent>()?.Publish(
                    //     new DeviceStateUpdatedMessage(true, deviceConfig));
                    return deviceConfig;

                Log.Error("LoadSettings: deviceConfig is null. The file content might be corrupted.");
            }
            catch (JsonException ex)
            {
                Log.Error($"LoadSettings: Failed to parse JSON. Exception: {ex.Message}");
            }
            catch (IOException ex)
            {
                Log.Error($"LoadSettings: File IO error. Exception: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error($"LoadSettings: Unexpected error. Exception: {ex.Message}");
            }

        // Default values if no settings file exists or an error occurs
        return new DeviceConfig
        {
            IpAddress = "",
            Username = "",
            Password = "",
            DeviceType = DeviceType.Camera,
        };
    }


    /// <summary>
    ///     Saves the device configuration settings to a JSON file.
    /// </summary>
    /// <param name="settings">The <see cref="DeviceConfig" /> object containing the settings to be saved.</param>
    /// <remarks>
    ///     This method serializes the provided <see cref="DeviceConfig" /> object into a JSON format and writes it to a file.
    /// </remarks>
    public static void SaveSettings(DeviceConfig settings)
    {
        var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
        File.WriteAllText(AppSettingFilename, json);
    }
}