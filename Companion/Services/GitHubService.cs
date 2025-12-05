using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace Companion.Services
{
    public class GitHubService : IGitHubService
    {
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;
        private readonly string _cacheKey = "GitHubData"; // Unique key for your data
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(120); // Cache for 1 hour
        private readonly ILogger _logger;
        
        public GitHubService(IMemoryCache cache, HttpClient httpClient, ILogger logger)
        {
            _logger = logger?.ForContext(GetType()) ?? 
                      throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<string> GetGitHubDataAsync(string url)
        {
            if (_cache.TryGetValue(_cacheKey, out string cachedData))
            {
                _logger.Information("GitHub API data retrieved from cache.");
                return cachedData;
            }

            // Data not in cache, fetch from GitHub API
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; OpenIPC-Config/1.0)");

            try
            {
                var response = await _httpClient.GetStringAsync(url);

                // Store the data in the cache
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(_cacheDuration) // Data expires after this time
                    .SetPriority(CacheItemPriority.Normal);   // Low priority for eviction

                _cache.Set(_cacheKey, response, cacheEntryOptions);
                _logger.Information("Data retrieved from GitHub API and cached.");
                return response;
            }
            catch (HttpRequestException ex)
            {
                // Handle API errors gracefully (log, throw, etc.)
                _logger.Error($"Error calling GitHub API: {ex.Message}");
                return null; // Or throw the exception, depending on your needs
            }
        }
    }
}
