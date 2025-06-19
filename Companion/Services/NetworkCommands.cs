using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public class NetworkCommands : INetworkCommands
{
    // Create a new Ping instance
    private readonly Ping ping;

    public NetworkCommands()
    {
        ping = new Ping();
    }

    public Task Run(DeviceConfig deviceConfig, string command)
    {
        throw new NotImplementedException();
    }

    public Task<bool> Ping(string ipAddress)
    {
        // Send a ping request
        var reply = ping.Send(ipAddress);

        // Check the status of the ping request
        if (reply.Status == IPStatus.Success)
        {
            Log.Verbose($"Ping successful: {reply.Status}");
            return Task.FromResult(true);
        }

        Log.Verbose($"Ping failed: {reply.Status}");
        return Task.FromResult(false);
    }
}