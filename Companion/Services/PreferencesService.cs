using System;
using System.IO;
using Companion.Models;
using Newtonsoft.Json;
using Serilog;

namespace Companion.Services;

public sealed class PreferencesService : IPreferencesService
{
    private readonly string _preferencesFilePath;
    private readonly ILogger _logger;

    public PreferencesService(ILogger logger)
        : this(logger, Path.Combine(OpenIPC.AppDataConfigDirectory, "preferences.json"))
    {
    }

    public PreferencesService(ILogger logger, string preferencesFilePath)
    {
        _logger = logger.ForContext<PreferencesService>();
        _preferencesFilePath = preferencesFilePath ?? throw new ArgumentNullException(nameof(preferencesFilePath));
    }

    public UserPreferences Load()
    {
        if (!File.Exists(_preferencesFilePath))
            return new UserPreferences();

        try
        {
            var json = File.ReadAllText(_preferencesFilePath);
            var preferences = JsonConvert.DeserializeObject<UserPreferences>(json);
            return preferences ?? new UserPreferences();
        }
        catch (JsonException ex)
        {
            _logger.Error(ex, "Failed to parse user preferences. Using defaults.");
            return new UserPreferences();
        }
        catch (IOException ex)
        {
            _logger.Error(ex, "Failed to read user preferences. Using defaults.");
            return new UserPreferences();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.Error(ex, "Access denied reading user preferences. Using defaults.");
            return new UserPreferences();
        }
    }

    public void Save(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var directory = Path.GetDirectoryName(_preferencesFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonConvert.SerializeObject(preferences, Formatting.Indented);
        File.WriteAllText(_preferencesFilePath, json);
    }
}
