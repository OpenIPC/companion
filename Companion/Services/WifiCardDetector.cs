using System;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Core;

namespace Companion.Services;

 public class WifiCardDetector
{
    public static string DetectWifiCard(string lsusbOutput)
    {
        // Parse the lsusb output to extract device IDs.  This is more robust than cutting by spaces.
        // Assumes each line in lsusbOutput contains "ID <vendorID>:<productID>"
        var deviceIds = lsusbOutput.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                Match match = Regex.Match(line, @"ID\s+([0-9a-f]{4}):([0-9a-f]{4})", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return $"{match.Groups[1].Value}:{match.Groups[2].Value}".ToLower(); // Convert to lowercase for consistent matching
                }
                return null;
            })
            .Where(id => !string.IsNullOrEmpty(id)) //Remove nulls
            .Distinct() // Mimic the "sort | uniq"
            .ToList();

        string driver = null;  // Initialize driver to null

        foreach (string card in deviceIds)
        {
            switch (card)
            {
                case "0bda:8812":
                case "0bda:881a":
                case "0b05:17d2":
                case "2357:0101":
                case "2604:0012":
                    driver = "88XXau";
                    Log.Information($"Detected WiFi card: {card}, Driver: {driver}"); // Log the detection
                    // Simulate modprobe with appropriate parameters (replace with actual C# equivalent if needed)
                    //Modprobe("88XXau", $"rtw_tx_pwr_idx_override={driver_txpower_override}"); //Replace with the C# call
                    break;

                case "0bda:a81a":
                    driver = "8812eu";
                    Log.Information($"Detected WiFi card: {card}, Driver: {driver}");
                    // Simulate modprobe with appropriate parameters
                    //Modprobe("8812eu", "rtw_regd_src=1 rtw_tx_pwr_by_rate=0 rtw_tx_pwr_lmt_enable=0"); //Replace with the C# call
                    break;

                case "0bda:f72b":
                case "0bda:b733":
                    driver = "8733bu";
                    Log.Information($"Read WiFi card: {card}, Driver: {driver}");
                    // Simulate modprobe with appropriate parameters
                    //Modprobe("8733bu", "rtw_regd_src=1 rtw_tx_pwr_by_rate=0 rtw_tx_pwr_lmt_enable=0"); //Replace with the C# call
                    break;

                case "0cf3:9271":
                case "040d:3801":
                    driver = "ar9271";
                    Log.Information($"Read WiFi card: {card}, Driver: {driver}");
                    break;
                case "0bda:c812":
                    driver = "88x2cu";
                    Log.Information($"Read WiFi card: {card}, Driver: {driver}");
                    break;
            }

           if (driver != null) break; //Stop at the first match (mimics the bash script behavior)
        }

        return driver; // Return the driver if found, otherwise null
    }


    
}   
