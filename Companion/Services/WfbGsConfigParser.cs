using System;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace Companion.Services;

public class WfbGsConfigParser : IWfbGsConfigParser
{
    // Store the original configuration content
    private string _originalConfigContent;

    // Properties to store parsed values
    public string TxPower { get; set; }

    // Method to generate the updated configuration string while preserving comments
    public string GetUpdatedConfigString()
    {
        if (string.IsNullOrEmpty(_originalConfigContent))
        {
            Log.Warning("Original config content is empty or null.");
            return string.Empty;
        }

        var lines = _originalConfigContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var updatedConfig = new StringBuilder();
        var optionsRegex = new Regex(@"^\s*options\s+88XXau_wfb\s+(.+)$");

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Check for the options line
            var optionsMatch = optionsRegex.Match(trimmedLine);
            if (optionsMatch.Success)
            {
                // Update the TxPower value in the options line
                var newOptionsLine = $"options 88XXau_wfb rtw_tx_pwr_idx_override={TxPower}";
                updatedConfig.AppendLine(newOptionsLine);
                Log.Information($"Updated TxPower to: {TxPower}");
            }
            else
            {
                // Preserve the original line (including comments)
                updatedConfig.AppendLine(line);
            }
        }

        var result = updatedConfig.ToString();
        Log.Information("Configuration string updated successfully.");
        Log.Debug($"Updated Config:\n{result}");
        return result;
    }

    // Method to parse configuration from a string
    public void ParseConfigString(string configContent)
    {
        if (string.IsNullOrWhiteSpace(configContent))
        {
            Log.Error("Config content is empty or null.");
            return;
        }

        _originalConfigContent = configContent;
        var lines = configContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Regular expressions to match key-value pairs and options line
        var keyValueRegex = new Regex(@"^\s*(\w+)\s*=\s*['""]?(.*?)['""]?\s*(?:#.*)?$");
        var optionsRegex = new Regex(@"^\s*options\s+88XXau_wfb\s+(.+)$");

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            // Check for options line (e.g., "options 88XXau_wfb rtw_tx_pwr_idx_override=1")
            var optionsMatch = optionsRegex.Match(trimmedLine);
            if (optionsMatch.Success)
            {
                var optionsValue = optionsMatch.Groups[1].Value;
                var index = optionsValue.IndexOf("rtw_tx_pwr_idx_override=");
                if (index != -1)
                {
                    // Extract the TxPower value (e.g., "1" from "rtw_tx_pwr_idx_override=1")
                    var powerValue = optionsValue.Substring(index + "rtw_tx_pwr_idx_override=".Length).Trim();
                    TxPower = powerValue;
                    Log.Debug($"Parsed TxPower: {TxPower}");
                }
            }
        }

        Log.Debug("WFB Configuration Parsed Successfully.");
    }
}