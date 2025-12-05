using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Serilog;

namespace Companion.Services;

public class WfbConfigParser
{
    // Properties to store parsed values
    public string Unit { get; private set; }
    public string Wlan { get; private set; }
    public string Region { get; private set; }
    public string Channel { get; private set; }
    public int TxPower { get; private set; }
    public int DriverTxPowerOverride { get; private set; }
    public int Bandwidth { get; private set; }
    public int Stbc { get; private set; }
    public int Ldpc { get; private set; }
    public int McsIndex { get; private set; }
    public int Stream { get; private set; }
    public long LinkId { get; private set; }
    public int UdpPort { get; private set; }
    public int RcvBuf { get; private set; }
    public string FrameType { get; private set; }
    public int FecK { get; private set; }
    public int FecN { get; private set; }
    public int PoolTimeout { get; private set; }
    public string GuardInterval { get; private set; }

    // Method to parse configuration from a string
    public void ParseConfigString(string configContent)
    {
        if (string.IsNullOrWhiteSpace(configContent))
        {
            Log.Error("Config content is empty or null.");
            return;
        }

        var lines = configContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var configDict = new Dictionary<string, string>();

        // Regular expression to match key-value pairs (e.g., key=value)
        var keyValueRegex = new Regex(@"^\s*(\w+)\s*=\s*['""]?(.*?)['""]?\s*(?:#.*)?$");

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#")) continue;

            // Match key-value pairs
            var match = keyValueRegex.Match(trimmedLine);
            if (match.Success)
            {
                var key = match.Groups[1].Value.Trim();
                var value = match.Groups[2].Value.Trim();

                // Store key-value pairs in a dictionary
                configDict[key] = value;
            }
        }

        // Set properties based on parsed values
        configDict.TryGetValue("unit", out var unit);
        Unit = unit;

        configDict.TryGetValue("wlan", out var wlan);
        Wlan = wlan;

        configDict.TryGetValue("region", out var region);
        Region = region;

        configDict.TryGetValue("channel", out var channel);
        Channel = channel;

        if (configDict.TryGetValue("txpower", out var txPower))
            TxPower = int.TryParse(txPower, out var value) ? value : 0;

        if (configDict.TryGetValue("driver_txpower_override", out var driverOverride))
            DriverTxPowerOverride = int.TryParse(driverOverride, out var value) ? value : 0;

        if (configDict.TryGetValue("bandwidth", out var bandwidth))
            Bandwidth = int.TryParse(bandwidth, out var value) ? value : 0;

        if (configDict.TryGetValue("stbc", out var stbc)) Stbc = int.TryParse(stbc, out var value) ? value : 0;

        if (configDict.TryGetValue("ldpc", out var ldpc)) Ldpc = int.TryParse(ldpc, out var value) ? value : 0;

        if (configDict.TryGetValue("mcs_index", out var mcsIndex))
            McsIndex = int.TryParse(mcsIndex, out var value) ? value : 0;

        if (configDict.TryGetValue("stream", out var stream)) Stream = int.TryParse(stream, out var value) ? value : 0;

        if (configDict.TryGetValue("link_id", out var linkId))
            LinkId = long.TryParse(linkId, out var value) ? value : 0;

        if (configDict.TryGetValue("udp_port", out var udpPort))
            UdpPort = int.TryParse(udpPort, out var value) ? value : 0;

        if (configDict.TryGetValue("rcv_buf", out var rcvBuf)) RcvBuf = int.TryParse(rcvBuf, out var value) ? value : 0;

        configDict.TryGetValue("frame_type", out var frameType);
        FrameType = frameType;

        if (configDict.TryGetValue("fec_k", out var fecK)) FecK = int.TryParse(fecK, out var value) ? value : 0;

        if (configDict.TryGetValue("fec_n", out var fecN)) FecN = int.TryParse(fecN, out var value) ? value : 0;

        if (configDict.TryGetValue("pool_timeout", out var poolTimeout))
            PoolTimeout = int.TryParse(poolTimeout, out var value) ? value : 0;

        configDict.TryGetValue("guard_interval", out var guardInterval);
        GuardInterval = guardInterval;

        // Log the parsed configuration
        Log.Debug("WFB Configuration Parsed Successfully:");
        Log.Debug($"Unit: {Unit}");
        Log.Debug($"Wlan: {Wlan}");
        Log.Debug($"Region: {Region}");
        Log.Debug($"Channel: {Channel}");
        Log.Debug($"TxPower: {TxPower}");
        Log.Debug($"DriverTxPowerOverride: {DriverTxPowerOverride}");
        Log.Debug($"Bandwidth: {Bandwidth}");
        Log.Debug($"Stbc: {Stbc}");
        Log.Debug($"Ldpc: {Ldpc}");
        Log.Debug($"McsIndex: {McsIndex}");
        Log.Debug($"Stream: {Stream}");
        Log.Debug($"LinkId: {LinkId}");
        Log.Debug($"UdpPort: {UdpPort}");
        Log.Debug($"RcvBuf: {RcvBuf}");
        Log.Debug($"FrameType: {FrameType}");
        Log.Debug($"FecK: {FecK}");
        Log.Debug($"FecN: {FecN}");
        Log.Debug($"PoolTimeout: {PoolTimeout}");
        Log.Debug($"GuardInterval: {GuardInterval}");
    }
}