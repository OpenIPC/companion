using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Companion.Services;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class OpenIpcDiscoveryServiceTests
{
    [Test]
    public void BuildDiscoveryScanPrefix_ClampsBroadSubnetToClassC()
    {
        var prefix = NetworkHelper.BuildDiscoveryScanPrefix(
            IPAddress.Parse("192.168.2.45"),
            IPAddress.Parse("255.255.0.0"));

        Assert.That(prefix, Is.EqualTo("192.168.2."));
    }

    [Test]
    public void SelectPreferredCandidates_PrefersUsbCandidatesOverVirtualNoGatewayAdapters()
    {
        var method = typeof(OpenIpcDiscoveryService).GetMethod(
            "SelectPreferredCandidates",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.That(method, Is.Not.Null);

        var candidates = new List<NetworkHelper.LocalNetworkCandidate>
        {
            new(
                IPAddress.Parse("172.22.224.1"),
                IPAddress.Parse("255.255.240.0"),
                "vEthernet (WSL)",
                35,
                false,
                false,
                true,
                false),
            new(
                IPAddress.Parse("192.168.2.10"),
                IPAddress.Parse("255.255.255.0"),
                "USB Ethernet/RNDIS Gadget",
                120,
                false,
                true,
                false,
                true)
        };

        var selected = (IReadOnlyList<NetworkHelper.LocalNetworkCandidate>)method!.Invoke(null, new object[] { candidates })!;

        Assert.That(selected.Select(candidate => candidate.InterfaceName).ToArray(),
            Is.EqualTo(new[] { "USB Ethernet/RNDIS Gadget" }));
    }
}
