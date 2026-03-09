using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;

namespace Companion.Services;

public class NetworkHelper
{
    public sealed record LocalNetworkCandidate(
        IPAddress IpAddress,
        IPAddress Mask,
        string InterfaceName,
        int Priority,
        bool HasGateway,
        bool IsUsbLike,
        bool IsVirtualLike,
        bool IsPrivateIPv4);

    public static string GetLocalIPAddress()
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var netInterface in networkInterfaces)
            {
                // Ignore loopback and inactive interfaces
                if (netInterface.OperationalStatus != OperationalStatus.Up ||
                    netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                var properties = netInterface.GetIPProperties();
                foreach (var address in properties.UnicastAddresses)
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork) // IPv4
                        return address.Address.ToString();
            }

            throw new Exception("No valid network interfaces found.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return null;
        }
    }

    public static (IPAddress ip, IPAddress mask)? GetPreferredLocalIPv4()
    {
        var preferred = GetLocalNetworkCandidates().FirstOrDefault();
        if (preferred == null)
            return null;

        return (preferred.IpAddress, preferred.Mask);
    }

    public static IReadOnlyList<LocalNetworkCandidate> GetLocalNetworkCandidates()
    {
        var candidates = new List<LocalNetworkCandidate>();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                continue;

            var properties = nic.GetIPProperties();
            var hasGateway = properties.GatewayAddresses
                .Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(g.Address));

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                if (IPAddress.IsLoopback(unicast.Address))
                    continue;

                if (unicast.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    continue;

                var mask = unicast.IPv4Mask ?? IPAddress.Parse("255.255.255.0");
                var isUsbLike = IsUsbLikeInterface(nic);
                var isVirtualLike = IsVirtualLikeInterface(nic);
                var isPrivateIPv4 = IsPrivateIPv4(unicast.Address);
                var priority = CalculatePriority(nic, unicast.Address, hasGateway, isUsbLike);
                candidates.Add(new LocalNetworkCandidate(
                    unicast.Address,
                    mask,
                    nic.Name,
                    priority,
                    hasGateway,
                    isUsbLike,
                    isVirtualLike,
                    isPrivateIPv4));
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.IpAddress.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string BuildScanPrefix(IPAddress ipAddress, IPAddress mask)
    {
        var octets = ipAddress.GetAddressBytes();
        var maskOctets = mask.GetAddressBytes();

        var prefixLength = 0;
        foreach (var octet in maskOctets)
        {
            for (var bit = 7; bit >= 0; bit--)
            {
                if ((octet & (1 << bit)) != 0)
                    prefixLength++;
            }
        }

        if (prefixLength <= 16)
            return $"{octets[0]}.{octets[1]}.";

        if (prefixLength <= 24)
            return $"{octets[0]}.{octets[1]}.{octets[2]}.";

        return $"{octets[0]}.{octets[1]}.{octets[2]}.{octets[3]}";
    }

    public static string BuildDiscoveryScanPrefix(IPAddress ipAddress, IPAddress mask)
    {
        var prefix = BuildScanPrefix(ipAddress, mask);
        var parts = prefix.Split('.', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3)
            return $"{ipAddress.GetAddressBytes()[0]}.{ipAddress.GetAddressBytes()[1]}.{ipAddress.GetAddressBytes()[2]}.";

        return parts.Length == 4
            ? $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}"
            : $"{parts[0]}.{parts[1]}.{parts[2]}.";
    }

    public static List<string> BuildScanTargets(string input)
    {
        var hosts = new List<string>();
        if (string.IsNullOrWhiteSpace(input))
            return hosts;

        var trimmed = input.Trim();
        if (trimmed.EndsWith(".", StringComparison.Ordinal))
            trimmed = trimmed.TrimEnd('.');

        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Length > 4)
            return hosts;

        if (!parts.All(part => int.TryParse(part, out var octet) && octet is >= 0 and <= 255))
            return hosts;

        if (parts.Length == 4)
        {
            hosts.Add(trimmed);
            return hosts;
        }

        if (parts.Length == 3)
        {
            var prefix = $"{parts[0]}.{parts[1]}.{parts[2]}.";
            for (var i = 1; i < 255; i++)
                hosts.Add(prefix + i);
            return hosts;
        }

        var twoOctetPrefix = $"{parts[0]}.{parts[1]}.";
        for (var third = 0; third <= 255; third++)
        {
            var thirdPrefix = $"{twoOctetPrefix}{third}.";
            for (var fourth = 1; fourth < 255; fourth++)
                hosts.Add(thirdPrefix + fourth);
        }

        return hosts;
    }

    private static bool IsPrivateIPv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4)
            return false;

        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    private static int CalculatePriority(NetworkInterface nic, IPAddress address, bool hasGateway, bool isUsbLike)
    {
        var score = 0;
        var descriptor = $"{nic.Name} {nic.Description} {nic.Id}";

        if (isUsbLike)
            score += 100;

        if (!hasGateway)
            score += 40;

        if (IsPrivateIPv4(address))
            score += 20;

        if (ContainsAny(descriptor, "bridge", "virtual", "vmware", "hyper-v", "vbox", "docker"))
            score -= 25;

        return score;
    }

    private static bool IsUsbLikeInterface(NetworkInterface nic)
    {
        var descriptor = $"{nic.Name} {nic.Description} {nic.Id}";
        return ContainsAny(descriptor, "usb", "serial", "rndis", "cdc", "ecm", "ncm");
    }

    private static bool IsVirtualLikeInterface(NetworkInterface nic)
    {
        var descriptor = $"{nic.Name} {nic.Description} {nic.Id}";
        return ContainsAny(descriptor,
            "bridge",
            "virtual",
            "vmware",
            "hyper-v",
            "vbox",
            "docker",
            "wsl",
            "vethernet",
            "default switch",
            "tailscale",
            "zerotier");
    }

    private static bool ContainsAny(string value, params string[] markers)
    {
        foreach (var marker in markers)
        {
            if (value.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
