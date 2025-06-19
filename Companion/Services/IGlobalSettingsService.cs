using System.Threading.Tasks;

namespace Companion.Services;

public interface IGlobalSettingsService
{
    bool IsWfbYamlEnabled { get; }
    Task ReadDevice();
}