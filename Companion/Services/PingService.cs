using System;
using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Companion.Services;

public class PingService
{
    private static PingService? _instance;
    private static readonly object _lock = new object();
    
    private readonly SemaphoreSlim _pingSemaphore = new SemaphoreSlim(1, 1);
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromMilliseconds(500);
    private readonly ILogger _logger;

    // Private constructor for singleton pattern
    private PingService(ILogger logger)
    {
        _logger = logger;
    }

    // Singleton instance getter
    public static PingService Instance(ILogger logger)
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    _instance = new PingService(logger);
                }
            }
        }
        return _instance!;
    }

    // Ping method with default timeout
    public async Task<PingReply> SendPingAsync(string ipAddress)
    {
        return await SendPingAsync(ipAddress, (int)_defaultTimeout.TotalMilliseconds);
    }

    // Ping method with custom timeout
    public async Task<PingReply> SendPingAsync(string ipAddress, int timeout)
    {
        // Log which IP is being pinged
        _logger.Verbose($"Attempting to ping IP: {ipAddress}");
        
        if (await _pingSemaphore.WaitAsync(timeout))
        {
            try
            {
                using (var ping = new Ping())
                {
                    return await ping.SendPingAsync(ipAddress, timeout);
                }
            }
            finally
            {
                // Release the semaphore when done
                _pingSemaphore.Release();
            }
        }
        else
        {
            _logger.Warning("Timeout waiting to acquire ping semaphore for {IpAddress}", ipAddress);

            // Since we can't create a PingReply directly, throw a meaningful exception instead
            throw new TimeoutException($"Ping operation to {ipAddress} was delayed due to concurrent requests");
        }
    }

    // Dispose method to clean up all resources
    public void Dispose()
    {
        _pingSemaphore.Dispose();
    }
}
