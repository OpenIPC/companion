using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public class OpenIpcDiscoveryService : IOpenIpcDiscoveryService
{
    private readonly ILogger _logger;
    private static readonly HttpClient ProbeHttpClient = CreateProbeHttpClient();
    private static readonly HttpClient AuthProbeHttpClient = CreateAuthProbeHttpClient();

    private static readonly string CacheFilePath =
        Path.Combine(OpenIPC.AppDataConfigDirectory, "discovered_devices.json");

    private readonly HashSet<string> _cachedHosts = new(StringComparer.OrdinalIgnoreCase);

    public OpenIpcDiscoveryService(ILogger logger)
    {
        _logger = logger.ForContext<OpenIpcDiscoveryService>();
        LoadCache();
    }

    public async Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: probe previously discovered hosts first
        if (_cachedHosts.Count > 0)
        {
            _logger.Information("Probing {Count} cached host(s) before full scan.", _cachedHosts.Count);
            var cachedResults = await ProbeCachedHostsAsync(cancellationToken);
            if (cachedResults.Count > 0)
            {
                _logger.Information("Found {Count} cached OpenIPC device(s), skipping full scan.", cachedResults.Count);
                return cachedResults;
            }
        }

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
                Prefix = NetworkHelper.BuildDiscoveryScanPrefix(candidate.IpAddress, candidate.Mask),
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

        if (ordered.Count > 0)
            UpdateCache(ordered);

        return ordered;
    }

    private async Task<IReadOnlyList<string>> ProbeCachedHostsAsync(CancellationToken cancellationToken)
    {
        var results = new List<string>();
        var tasks = _cachedHosts.Select(async host =>
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1500));
                if (await LooksLikeOpenIpcAsync(host, timeoutCts.Token))
                {
                    lock (results)
                    {
                        results.Add(host);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Cached host probe failed for {Host}.", host);
            }
        });

        await Task.WhenAll(tasks);
        return results.OrderBy(ip => ip, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void UpdateCache(IEnumerable<string> hosts)
    {
        foreach (var host in hosts)
            _cachedHosts.Add(host);

        try
        {
            var json = JsonSerializer.Serialize(_cachedHosts.ToList());
            File.WriteAllText(CacheFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to persist discovery cache.");
        }
    }

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
                return;

            var json = File.ReadAllText(CacheFilePath);
            var hosts = JsonSerializer.Deserialize<List<string>>(json);
            if (hosts == null) return;

            foreach (var host in hosts)
                _cachedHosts.Add(host);

            _logger.Information("Loaded {Count} cached host(s) from discovery cache.", _cachedHosts.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to load discovery cache.");
        }
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

        if (ContainsOpenIpcMarker(headers) ||
            ContainsOpenIpcMarker(server) ||
            ContainsOpenIpcMarker(authHeader) ||
            ContainsOpenIpcMarker(body))
            return true;

        // If the device returned 401 Basic auth, try with default OpenIPC credentials
        // to confirm it's actually an OpenIPC device and not a false positive
        if ((int)response.StatusCode == 401 &&
            authHeader.Contains("Basic", StringComparison.OrdinalIgnoreCase))
            return await LooksLikeOpenIpcWithCredentialsAsync(host, timeoutCts.Token);

        return false;
    }

    private async Task<bool> LooksLikeOpenIpcWithCredentialsAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://{host}/");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes("root:12345")));
            using var response = await AuthProbeHttpClient.SendAsync(request,
                HttpCompletionOption.ResponseContentRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var headers = string.Join(" ", response.Headers.SelectMany(h => h.Value));
            var server = response.Headers.Server.ToString();

            return ContainsOpenIpcMarker(body) ||
                   ContainsOpenIpcMarker(headers) ||
                   ContainsOpenIpcMarker(server);
        }
        catch
        {
            return false;
        }
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

    private static HttpClient CreateAuthProbeHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true
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
        var usbCandidates = candidates
            .Where(candidate => candidate.IsUsbLike && !candidate.IsVirtualLike)
            .ToList();

        if (usbCandidates.Count > 0)
            return usbCandidates;

        if (OperatingSystem.IsMacOS())
        {
            var macBridgeCandidates = candidates
                .Where(candidate => candidate.InterfaceName.Equals("bridge100", StringComparison.OrdinalIgnoreCase) &&
                                    candidate.IsPrivateIPv4)
                .ToList();

            if (macBridgeCandidates.Count > 0)
                return macBridgeCandidates;
        }

        var directAttached = candidates
            .Where(candidate => !candidate.HasGateway &&
                                !candidate.IsVirtualLike &&
                                candidate.IsPrivateIPv4)
            .ToList();

        if (!OperatingSystem.IsWindows() && directAttached.Count > 0)
            return directAttached;

        var primaryCandidates = candidates
            .Where(candidate => !candidate.IsVirtualLike && candidate.IsPrivateIPv4)
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.InterfaceName, StringComparer.OrdinalIgnoreCase)
            .Take(OperatingSystem.IsWindows() ? 2 : 1)
            .ToList();

        if (primaryCandidates.Count > 0)
            return primaryCandidates;

        return candidates.Take(1).ToList();
    }

}
