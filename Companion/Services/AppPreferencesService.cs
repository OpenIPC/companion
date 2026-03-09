using System;
using System.IO;
using Companion.Models;
using Newtonsoft.Json;
using Serilog;

namespace Companion.Services;

public class AppPreferencesService : IAppPreferencesService
{
    private readonly ILogger _logger;

    public static string PreferencesFilename { get; set; } =
        Path.Combine(OpenIPC.AppDataConfigDirectory, "openipc_preferences.json");

    public AppPreferencesService(ILogger logger)
    {
        _logger = logger.ForContext<AppPreferencesService>();
    }

    public AppPreferences Load()
    {
        if (!File.Exists(PreferencesFilename))
        {
            var defaults = new AppPreferences();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(PreferencesFilename);
            return JsonConvert.DeserializeObject<AppPreferences>(json) ?? new AppPreferences();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load app preferences from {PreferencesFilename}", PreferencesFilename);
            return new AppPreferences();
        }
    }

    public void Save(AppPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesFilename)!);
            var json = JsonConvert.SerializeObject(preferences, Formatting.Indented);
            File.WriteAllText(PreferencesFilename, json);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save app preferences to {PreferencesFilename}", PreferencesFilename);
        }
    }
}
