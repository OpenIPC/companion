using System.Threading.Tasks;
using Companion.Models;

namespace Companion.Services;

public interface INetworkCommands
{
    extern Task<bool> Ping(DeviceConfig deviceConfig);

    Task Run(DeviceConfig deviceConfig, string command);
}