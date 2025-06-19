using System.Threading.Tasks;

namespace Companion.Services;

public interface IGitHubService
{
    Task<string> GetGitHubDataAsync(string url);
}