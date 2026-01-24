using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace Companion.Services;

public class WifiConfigParser
{
    private readonly Dictionary<string, string> _configDict = new();

    // Store the original configuration content
    private string _originalConfigContent = string.Empty;

    // Properties to store parsed values
    public int WifiChannel { get; set; }
    public string WifiRegion { get; set; } = string.Empty;
    public string GsMavlinkPeer { get; set; } = string.Empty;
    public string GsVideoPeer { get; set; } = string.Empty;

    // Method to parse configuration from a string
    public void ParseConfigString(string configContent)
    {
        if (string.IsNullOrWhiteSpace(configContent))
        {
            Log.Warning("Config content is empty or null.");
            return;
        }

        _originalConfigContent = configContent;
        var lines = configContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Regular expressions to match key-value pairs and section headers
        var keyValueRegex = new Regex(@"^\s*(\w+)\s*=\s*['""]?(.*?)['""]?\s*(?:#.*)?$");
        var sectionRegex = new Regex(@"^\s*\[.*\]\s*$");

        string? currentSection = null;

        foreach (var line in lines)
        {
            // Skip empty lines and section headers
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (sectionRegex.IsMatch(line))
            {
                currentSection = line.Trim();
                continue;
            }

            // Match key-value pairs
            var match = keyValueRegex.Match(line);
            if (match.Success)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();

                // Store the key-value pair in the dictionary
                _configDict[key] = value;

                // Update properties based on the parsed values
                switch (key)
                {
                    case "wifi_channel":
                        WifiChannel = int.TryParse(value, out var channel) ? channel : 0;
                        break;
                    case "wifi_region":
                        WifiRegion = value;
                        break;
                    case "peer" when currentSection == "[gs_mavlink]":
                        GsMavlinkPeer = value;
                        break;
                    case "peer" when currentSection == "[gs_video]":
                        GsVideoPeer = value;
                        break;
                }
            }
        }

        Log.Debug("Configuration Parsed Successfully:");
        Log.Debug($"Wifi Channel: {WifiChannel}");
        Log.Debug($"Wifi Region: {WifiRegion}");
        Log.Debug($"GS Mavlink Peer: {GsMavlinkPeer}");
        Log.Debug($"GS Video Peer: {GsVideoPeer}");
    }

    // Method to update the configuration content while preserving comments
    public string GetUpdatedConfigString()
    {
        if (string.IsNullOrEmpty(_originalConfigContent))
        {
            Log.Warning("Original config content is empty or null.");
            return string.Empty;
        }

        var lines = _originalConfigContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var updatedConfig = new StringBuilder();
        var keyValueRegex = new Regex(@"^\s*(\w+)\s*=\s*['""]?(.*?)['""]?\s*(#.*)?$");
        var sectionRegex = new Regex(@"^\s*\[.*\]\s*$");
        string? currentSection = null;

        foreach (var line in lines)
        {
            // Preserve section headers and empty lines
            if (sectionRegex.IsMatch(line) || string.IsNullOrWhiteSpace(line))
            {
                updatedConfig.AppendLine(line);
                if (sectionRegex.IsMatch(line)) currentSection = line.Trim();
                continue;
            }

            // Match key-value pairs
            var match = keyValueRegex.Match(line);
            if (match.Success)
            {
                var key = match.Groups[1].Value.Trim();
                var comment = match.Groups[3].Value;

                // Determine the new value for the key if it needs updating
                var newValue = match.Groups[2].Value.Trim();
                switch (key)
                {
                    case "wifi_channel":
                        newValue = WifiChannel.ToString();
                        break;
                    case "wifi_region":
                        newValue = "'" + WifiRegion + "'";
                        break;
                    case "peer" when currentSection == "[gs_mavlink]":
                        newValue = "'" + GsMavlinkPeer + "'";
                        break;
                    case "peer" when currentSection == "[gs_video]":
                        newValue = "'" + GsVideoPeer + "'";
                        break;
                }

                // Reconstruct the line with the updated value while preserving the comment
                updatedConfig.AppendLine($"{key} = {newValue} {comment}");
            }
            else
            {
                // Preserve lines that do not match key-value pairs (e.g., comments)
                updatedConfig.AppendLine(line);
            }
        }

        var result = updatedConfig.ToString();
        Log.Information("Updated configuration string built successfully.");
        Log.Debug($"Updated Config:\n{result}");
        return result;
    }
}
