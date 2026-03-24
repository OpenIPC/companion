using System;
using System.Collections.Generic;

namespace Companion.Models;

public sealed class HeraldDiscoveredDevice
{
    public const string DefaultServiceType = "_ssh._tcp.local";

    public string InstanceName { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public int Port { get; init; }
    public Dictionary<string, string> TxtRecords { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string Vendor =>
        TxtRecords.TryGetValue("vendor", out var vendor) ? vendor : string.Empty;

    public string Model =>
        TxtRecords.TryGetValue("model", out var model) ? model : string.Empty;

    public int SshPort =>
        TxtRecords.TryGetValue("ssh", out var sshPort) && int.TryParse(sshPort, out var value)
            ? value
            : Port;

    public DeviceType DeviceType
    {
        get
        {
            return DeviceType.Camera;
        }
    }

    public string DisplayName
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(InstanceName) ? Hostname : InstanceName;
            var detail = !string.IsNullOrWhiteSpace(Model) ? Model : "OpenIPC";
            return $"{label} ({IpAddress}:{SshPort}, {detail})";
        }
    }

    public override string ToString()
    {
        return DisplayName;
    }
}
