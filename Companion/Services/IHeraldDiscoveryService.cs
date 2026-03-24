using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;

namespace Companion.Services;

public interface IHeraldDiscoveryService
{
    Task<IReadOnlyList<HeraldDiscoveredDevice>> DiscoverAsync(
        string serviceType = HeraldDiscoveredDevice.DefaultServiceType,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
