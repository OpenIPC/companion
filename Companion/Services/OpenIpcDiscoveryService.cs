using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public class OpenIpcDiscoveryService : IOpenIpcDiscoveryService
{
    private readonly ILogger _logger;
    private static readonly HttpClient ProbeHttpClient = CreateProbeHttpClient();

    public OpenIpcDiscoveryService(ILogger logger)
    {
        _logger = logger.ForContext<OpenIpcDiscoveryService>();
    }

    public async Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var candidates = NetworkHelper.GetLocalNetworkCandidates();
        if (candidates.Count == 0)
        {
            _logger.Warning("No active IPv4 network was available for OpenIPC discovery.");
            return Array.Empty<string>();
        }

        var preferredCandidates = SelectPreferredCandidates(candidates);

        var scanGroups = preferredCandidates
            .Select(candidate => new
            {
                Prefix = NetworkHelper.BuildScanPrefix(candidate.IpAddress, candidate.Mask),
                candidate.InterfaceName,
                candidate.Priority
            })
            .GroupBy(candidate => candidate.Prefix, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.InterfaceName, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToList();

        var hosts = scanGroups
            .SelectMany(group => NetworkHelper.BuildScanTargets(group.Prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.Information("Scanning {HostCount} local hosts for OpenIPC devices across prefixes: {Prefixes}.",
            hosts.Count, string.Join(", ", scanGroups.Select(group => $"{group.Prefix} ({group.InterfaceName})")));

        var semaphore = new SemaphoreSlim(24);
        var discovered = new List<string>();
        var tasks = hosts.Select(async host =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (await LooksLikeOpenIpcAsync(host, cancellationToken))
                {
                    lock (discovered)
                    {
                        discovered.Add(host);
                    }
                }
            }
            catch (HttpRequestException ex) when (IsExpectedProbeFailure(ex))
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "OpenIPC discovery probe failed for host {Host}.", host);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var ordered = discovered
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ip => ip, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.Information("OpenIPC discovery finished with {DeviceCount} matches.", ordered.Count);
        return ordered;
    }

    private static async Task<bool> IsReachableAsync(string host, CancellationToken cancellationToken)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(host, 350);
        cancellationToken.ThrowIfCancellationRequested();
        return reply.Status == IPStatus.Success;
    }

    private async Task<bool> LooksLikeOpenIpcAsync(string host, CancellationToken cancellationToken)
    {
        var pingReachable = await TryPingAsync(host, cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(pingReachable ? TimeSpan.FromMilliseconds(750) : TimeSpan.FromMilliseconds(1200));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}/");
        using var response = await ProbeHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token);

        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 401)
            return false;

        var headers = string.Join(" ", response.Headers.SelectMany(header => header.Value));
        var server = response.Headers.Server.ToString();
        var authHeader = string.Join(" ", response.Headers.WwwAuthenticate.Select(header => header.ToString()));
        var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);

        return ContainsOpenIpcMarker(headers) ||
               ContainsOpenIpcMarker(server) ||
               ContainsOpenIpcMarker(authHeader) ||
               LooksLikeBasicAuthDevice(response, authHeader) ||
               ContainsOpenIpcMarker(body);
    }

    private static bool ContainsOpenIpcMarker(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        return content.Contains("openipc", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("majestic", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("ipcam", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExpectedProbeFailure(HttpRequestException ex)
    {
        return ex.InnerException is AuthenticationException ||
               ex.InnerException is SocketException ||
               ex.Message.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("Host is down", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("response ended prematurely", StringComparison.OrdinalIgnoreCase) ||
               ex.Message.Contains("SSL connection could not be established", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateProbeHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };

        return new HttpClient(handler, disposeHandler: true);
    }

    private async Task<bool> TryPingAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await IsReachableAsync(host, cancellationToken);
        }
        catch (PingException ex)
        {
            _logger.Debug(ex, "ICMP ping failed for host {Host}; continuing with HTTP probe.", host);
            return false;
        }
        catch (SocketException ex)
        {
            _logger.Debug(ex, "Socket check failed for host {Host}; continuing with HTTP probe.", host);
            return false;
        }
    }

    private static IReadOnlyList<NetworkHelper.LocalNetworkCandidate> SelectPreferredCandidates(
        IReadOnlyList<NetworkHelper.LocalNetworkCandidate> candidates)
    {
        var directAttached = candidates
            .Where(candidate => candidate.IsUsbLike || !candidate.HasGateway)
            .ToList();

        if (directAttached.Count > 0)
            return directAttached;

        return candidates.Take(1).ToList();
    }

    private static bool LooksLikeBasicAuthDevice(HttpResponseMessage response, string authHeader)
    {
        return (int)response.StatusCode == 401 &&
               authHeader.Contains("Basic", StringComparison.OrdinalIgnoreCase);
    }
}
