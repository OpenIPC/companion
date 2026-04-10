using System;
using System.IO;
using Newtonsoft.Json;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public static class SettingsManager
{
    private static readonly string AppSettingsName = "openipc_settings.json";
    private const string DefaultIpAddress = "192.168.1.10";
    private const string DefaultUsername = "root";
    private const string DefaultPassword = "12345";

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
        if (File.Exists(AppSettingFilename))
            try
            {
                var json = File.ReadAllText(AppSettingFilename);
                var deviceConfig = JsonConvert.DeserializeObject<DeviceConfig>(json);

                if (deviceConfig != null)
                    return NormalizeSettings(deviceConfig);

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
        return CreateDefaultSettings();
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
        var normalizedSettings = NormalizeSettings(settings);
        var json = JsonConvert.SerializeObject(normalizedSettings, Formatting.Indented);
        File.WriteAllText(AppSettingFilename, json);
    }

    private static DeviceConfig CreateDefaultSettings()
    {
        return new DeviceConfig
        {
            IpAddress = DefaultIpAddress,
            Username = DefaultUsername,
            Password = DefaultPassword,
            DeviceType = DeviceType.Camera,
        };
    }

    private static DeviceConfig NormalizeSettings(DeviceConfig settings)
    {
        settings.IpAddress = string.IsNullOrWhiteSpace(settings.IpAddress)
            ? DefaultIpAddress
            : settings.IpAddress;

        settings.Username = string.IsNullOrWhiteSpace(settings.Username)
            ? DefaultUsername
            : settings.Username;

        settings.Password = string.IsNullOrWhiteSpace(settings.Password)
            ? DefaultPassword
            : settings.Password;

        if (settings.DeviceType == DeviceType.None)
            settings.DeviceType = DeviceType.Camera;

        settings.CachedIpAddresses ??= new();
        return settings;
    }
}
