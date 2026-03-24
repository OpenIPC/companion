using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

const string defaultServiceType = "_ssh._tcp.local";
const int mdnsPort = 5353;
var multicastAddress = IPAddress.Parse("224.0.0.251");

var instance = args.Length > 0 ? args[0] : "openipc-test";
var serviceType = args.Length > 1 ? args[1] : defaultServiceType;
var advertisedPort = args.Length > 2 && int.TryParse(args[2], out var parsedPort) ? parsedPort : 22;
var localIp = args.Length > 3 && IPAddress.TryParse(args[3], out var parsedIp) ? parsedIp : GetPreferredIPv4();
var model = args.Length > 4 ? args[4] : "general";

if (localIp == null)
{
    Console.Error.WriteLine("Unable to determine a local IPv4 address.");
    return 1;
}

var hostname = $"{instance}.local";
var instanceFqdn = $"{instance}.{serviceType}";
var txtEntries = new[]
{
    "vendor=OpenIPC",
    $"model={model}"
};

using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
socket.Bind(new IPEndPoint(IPAddress.Any, mdnsPort));
socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
    new MulticastOption(multicastAddress, IPAddress.Any));

var endpoint = new IPEndPoint(multicastAddress, mdnsPort);
var announcement = BuildAnnouncement(instanceFqdn, serviceType, hostname, localIp, advertisedPort, txtEntries);

Console.WriteLine($"Broadcasting {instanceFqdn} at {localIp}:{advertisedPort}");
Console.WriteLine("Press Ctrl+C to stop.");

using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var listenerTask = ListenAsync(socket, endpoint, serviceType, announcement, cts.Token);
var announceTask = AnnounceAsync(socket, endpoint, announcement, cts.Token, timer);

await Task.WhenAll(listenerTask, announceTask);
return 0;

static async Task ListenAsync(
    Socket socket,
    EndPoint endpoint,
    string serviceType,
    byte[] announcement,
    CancellationToken cancellationToken)
{
    var buffer = new byte[1500];

    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            var remote = new IPEndPoint(IPAddress.Any, 0) as EndPoint;
            var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, cancellationToken);
            var request = buffer.AsSpan(0, result.ReceivedBytes).ToArray();
            if (ContainsName(request, serviceType) || ContainsName(request, "_services._dns-sd._udp.local"))
                await socket.SendToAsync(announcement, SocketFlags.None, endpoint, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}

static async Task AnnounceAsync(
    Socket socket,
    EndPoint endpoint,
    byte[] announcement,
    CancellationToken cancellationToken,
    PeriodicTimer timer)
{
    await socket.SendToAsync(announcement, SocketFlags.None, endpoint, cancellationToken);

    while (await timer.WaitForNextTickAsync(cancellationToken))
        await socket.SendToAsync(announcement, SocketFlags.None, endpoint, cancellationToken);
}

static bool ContainsName(byte[] packet, string value)
{
    return Encoding.UTF8.GetString(packet).Contains(value, StringComparison.OrdinalIgnoreCase);
}

static byte[] BuildAnnouncement(
    string instanceFqdn,
    string serviceType,
    string hostname,
    IPAddress address,
    int port,
    IEnumerable<string> txtEntries)
{
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);

    WriteUInt16(writer, 0);
    WriteUInt16(writer, 0x8400);
    WriteUInt16(writer, 0);
    WriteUInt16(writer, 4);
    WriteUInt16(writer, 0);
    WriteUInt16(writer, 0);

    WritePtrRecord(writer, serviceType, instanceFqdn);
    WriteSrvRecord(writer, instanceFqdn, hostname, (ushort)port);
    WriteTxtRecord(writer, instanceFqdn, txtEntries);
    WriteARecord(writer, hostname, address);

    return stream.ToArray();
}

static void WritePtrRecord(BinaryWriter writer, string name, string target)
{
    WriteName(writer, name);
    WriteUInt16(writer, 12);
    WriteUInt16(writer, 1);
    WriteUInt32(writer, 120);

    using var rdata = new MemoryStream();
    using var rdataWriter = new BinaryWriter(rdata);
    WriteName(rdataWriter, target);

    WriteUInt16(writer, (ushort)rdata.Length);
    writer.Write(rdata.ToArray());
}

static void WriteSrvRecord(BinaryWriter writer, string name, string hostname, ushort port)
{
    WriteName(writer, name);
    WriteUInt16(writer, 33);
    WriteUInt16(writer, 1);
    WriteUInt32(writer, 120);

    using var rdata = new MemoryStream();
    using var rdataWriter = new BinaryWriter(rdata);
    WriteUInt16(rdataWriter, 0);
    WriteUInt16(rdataWriter, 0);
    WriteUInt16(rdataWriter, port);
    WriteName(rdataWriter, hostname);

    WriteUInt16(writer, (ushort)rdata.Length);
    writer.Write(rdata.ToArray());
}

static void WriteTxtRecord(BinaryWriter writer, string name, IEnumerable<string> entries)
{
    WriteName(writer, name);
    WriteUInt16(writer, 16);
    WriteUInt16(writer, 1);
    WriteUInt32(writer, 120);

    var payload = new List<byte>();
    foreach (var entry in entries)
    {
        var bytes = Encoding.UTF8.GetBytes(entry);
        payload.Add((byte)bytes.Length);
        payload.AddRange(bytes);
    }

    WriteUInt16(writer, (ushort)payload.Count);
    writer.Write(payload.ToArray());
}

static void WriteARecord(BinaryWriter writer, string hostname, IPAddress address)
{
    WriteName(writer, hostname);
    WriteUInt16(writer, 1);
    WriteUInt16(writer, 1);
    WriteUInt32(writer, 120);
    WriteUInt16(writer, 4);
    writer.Write(address.GetAddressBytes());
}

static void WriteName(BinaryWriter writer, string name)
{
    foreach (var label in name.Trim('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
    {
        var bytes = Encoding.UTF8.GetBytes(label);
        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    writer.Write((byte)0);
}

static void WriteUInt16(BinaryWriter writer, ushort value)
{
    writer.Write(BinaryPrimitives.ReverseEndianness(value));
}

static void WriteUInt32(BinaryWriter writer, uint value)
{
    writer.Write(BinaryPrimitives.ReverseEndianness(value));
}

static IPAddress? GetPreferredIPv4()
{
    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
    {
        if (nic.OperationalStatus != OperationalStatus.Up ||
            nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
            nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            continue;

        var properties = nic.GetIPProperties();
        foreach (var address in properties.UnicastAddresses)
        {
            if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address.Address))
                return address.Address;
        }
    }

    return null;
}
