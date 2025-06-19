using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Companion.Services;

public class NetworkHelper
{
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
}