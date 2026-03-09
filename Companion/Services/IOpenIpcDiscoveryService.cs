using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Companion.Services;

public interface IOpenIpcDiscoveryService
{
    Task<IReadOnlyList<string>> DiscoverAsync(CancellationToken cancellationToken = default);
}
