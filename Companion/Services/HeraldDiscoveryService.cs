using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public class HeraldDiscoveryService : IHeraldDiscoveryService
{
    private const int MdnsPort = 5353;
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.0.251");
    private readonly ILogger _logger;

    private sealed record DiscoveryInterface(string Name, int Index, IPAddress Address);
    private sealed class DiscoveryRecords
    {
        public List<(string Name, string Target)> PtrRecords { get; } = new();
        public Dictionary<string, (string Host, int Port)> SrvRecords { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Dictionary<string, string>> TxtRecords { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ARecords { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> InstanceRemoteAddresses { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public HeraldDiscoveryService(ILogger logger)
    {
        _logger = logger.ForContext<HeraldDiscoveryService>();
    }

    public async Task<IReadOnlyList<HeraldDiscoveredDevice>> DiscoverAsync(
        string serviceType = HeraldDiscoveredDevice.DefaultServiceType,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var devices = new Dictionary<string, HeraldDiscoveredDevice>(StringComparer.OrdinalIgnoreCase);
        var discoveryTimeout = timeout ?? TimeSpan.FromSeconds(3);
        var discoveryInterfaces = GetDiscoveryInterfaces();
        var discoveryRecords = new DiscoveryRecords();
        var queriedInstances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queriedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var discoveryInterface in discoveryInterfaces)
        {
            _logger.Debug("Using discovery interface {InterfaceName} (index {InterfaceIndex}) with address {LocalAddress}",
                discoveryInterface.Name,
                discoveryInterface.Index,
                discoveryInterface.Address);
        }

        using var udpClient = CreateMdnsClient(discoveryInterfaces);
        var query = BuildPtrQuery(serviceType);

        foreach (var discoveryInterface in discoveryInterfaces)
        {
            try
            {
                await SendQueryAsync(query, discoveryInterface, cancellationToken);
                _logger.Debug("Sent Herald discovery query for {ServiceType} via {InterfaceName} ({LocalAddress})",
                    serviceType, discoveryInterface.Name, discoveryInterface.Address);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed sending Herald query via {InterfaceName} ({LocalAddress})",
                    discoveryInterface.Name, discoveryInterface.Address);
            }
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(discoveryTimeout);

        while (!linkedCts.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(linkedCts.Token);
                _logger.Debug("Received mDNS response from {RemoteAddress}:{RemotePort} with {ByteCount} bytes",
                    result.RemoteEndPoint.Address,
                    result.RemoteEndPoint.Port,
                    result.Buffer.Length);
                var packetRecords = ParsePacket(result.Buffer, result.RemoteEndPoint);
                MergeRecords(discoveryRecords, packetRecords);

                await SendFollowUpQueriesAsync(
                    udpClient,
                    discoveryInterfaces,
                    discoveryRecords,
                    queriedInstances,
                    queriedHosts,
                    cancellationToken);

                var discovered = MaterializeDevices(discoveryRecords, serviceType);

                if (discovered.Count == 0)
                {
                    _logger.Debug("mDNS response from {RemoteAddress} yielded no {ServiceType} devices",
                        result.RemoteEndPoint.Address,
                        serviceType);
                }

                foreach (var device in discovered)
                {
                    _logger.Debug(
                        "Parsed Herald device Instance={InstanceName} Hostname={Hostname} Ip={IpAddress} Port={Port} Vendor={Vendor} Model={Model}",
                        device.InstanceName,
                        device.Hostname,
                        device.IpAddress,
                        device.SshPort,
                        device.Vendor,
                        device.Model);
                    var key = $"{device.InstanceName}|{device.IpAddress}";
                    devices[key] = device;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to parse mDNS response");
            }
        }

        return devices.Values
            .OrderBy(device => device.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<HeraldDiscoveredDevice> ParseResponse(
        byte[] buffer,
        IPEndPoint remoteEndPoint,
        string requestedServiceType)
    {
        var records = ParsePacket(buffer, remoteEndPoint);
        return MaterializeDevices(records, requestedServiceType);
    }

    private static UdpClient CreateMdnsClient(IReadOnlyList<DiscoveryInterface> discoveryInterfaces)
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, MdnsPort));

        var client = new UdpClient { Client = socket };
        foreach (var discoveryInterface in discoveryInterfaces)
            client.JoinMulticastGroup(MulticastAddress, discoveryInterface.Address);

        return client;
    }

    private static async Task SendQueryAsync(
        byte[] query,
        DiscoveryInterface discoveryInterface,
        CancellationToken cancellationToken)
    {
        using var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        sender.Bind(new IPEndPoint(discoveryInterface.Address, 0));
        sender.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface,
            discoveryInterface.Address.GetAddressBytes());
        await sender.SendToAsync(query, SocketFlags.None, new IPEndPoint(MulticastAddress, MdnsPort), cancellationToken);
    }

    private static List<DiscoveryInterface> GetDiscoveryInterfaces()
    {
        var interfaces = new List<DiscoveryInterface>();

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                !networkInterface.SupportsMulticast ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                continue;

            var properties = networkInterface.GetIPProperties();
            var ipv4Properties = properties.GetIPv4Properties();
            if (ipv4Properties == null)
                continue;

            foreach (var unicastAddress in properties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork ||
                    IPAddress.IsLoopback(unicastAddress.Address))
                    continue;

                interfaces.Add(new DiscoveryInterface(
                    networkInterface.Name,
                    ipv4Properties.Index,
                    unicastAddress.Address));
            }
        }

        if (interfaces.Count == 0)
            interfaces.Add(new DiscoveryInterface("any", 0, IPAddress.Any));

        return interfaces;
    }

    private static byte[] BuildPtrQuery(string serviceType)
    {
        return BuildQuery(serviceType, 12);
    }

    private static byte[] BuildSrvQuery(string instanceName)
    {
        return BuildQuery(instanceName, 33);
    }

    private static byte[] BuildTxtQuery(string instanceName)
    {
        return BuildQuery(instanceName, 16);
    }

    private static byte[] BuildAddressQuery(string hostName)
    {
        return BuildQuery(hostName, 1);
    }

    private static byte[] BuildQuery(string name, ushort type)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteUInt16(writer, 0);
        WriteUInt16(writer, 0);
        WriteUInt16(writer, 1);
        WriteUInt16(writer, 0);
        WriteUInt16(writer, 0);
        WriteUInt16(writer, 0);

        WriteName(writer, name);
        WriteUInt16(writer, type);
        WriteUInt16(writer, 1);

        return stream.ToArray();
    }

    private static DiscoveryRecords ParsePacket(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        var records = new DiscoveryRecords();
        var reader = new DnsReader(buffer);
        if (buffer.Length < 12)
            return records;

        reader.SkipUInt16();
        reader.SkipUInt16();
        var questionCount = reader.ReadUInt16();
        var answerCount = reader.ReadUInt16();
        var authorityCount = reader.ReadUInt16();
        var additionalCount = reader.ReadUInt16();

        for (var i = 0; i < questionCount; i++)
        {
            reader.ReadName();
            reader.SkipUInt16();
            reader.SkipUInt16();
        }

        var totalRecords = answerCount + authorityCount + additionalCount;
        for (var i = 0; i < totalRecords; i++)
        {
            var name = NormalizeDnsName(reader.ReadName());
            var type = reader.ReadUInt16();
            reader.SkipUInt16();
            reader.SkipUInt32();
            var rdLength = reader.ReadUInt16();
            var rdataOffset = reader.Position;

            switch (type)
            {
                case 1 when rdLength == 4:
                    records.ARecords[name] = new IPAddress(reader.ReadBytes(rdLength)).ToString();
                    break;
                case 12:
                    var target = NormalizeDnsName(reader.ReadName());
                    records.PtrRecords.Add((name, target));
                    records.InstanceRemoteAddresses[target] = remoteEndPoint.Address.ToString();
                    break;
                case 16:
                    records.TxtRecords[name] = ReadTxtRecord(reader.ReadBytes(rdLength));
                    break;
                case 33:
                    reader.SkipUInt16();
                    reader.SkipUInt16();
                    var port = reader.ReadUInt16();
                    var targetHost = NormalizeDnsName(reader.ReadName());
                    records.SrvRecords[name] = (targetHost, port);
                    break;
                default:
                    reader.Skip(rdLength);
                    break;
            }

            reader.Position = rdataOffset + rdLength;
        }

        return records;
    }

    private static void MergeRecords(DiscoveryRecords aggregate, DiscoveryRecords packet)
    {
        foreach (var ptrRecord in packet.PtrRecords)
        {
            if (!aggregate.PtrRecords.Contains(ptrRecord))
                aggregate.PtrRecords.Add(ptrRecord);
        }

        foreach (var srvRecord in packet.SrvRecords)
            aggregate.SrvRecords[srvRecord.Key] = srvRecord.Value;

        foreach (var txtRecord in packet.TxtRecords)
            aggregate.TxtRecords[txtRecord.Key] = txtRecord.Value;

        foreach (var aRecord in packet.ARecords)
            aggregate.ARecords[aRecord.Key] = aRecord.Value;

        foreach (var instanceRemoteAddress in packet.InstanceRemoteAddresses)
            aggregate.InstanceRemoteAddresses[instanceRemoteAddress.Key] = instanceRemoteAddress.Value;
    }

    private static IReadOnlyList<HeraldDiscoveredDevice> MaterializeDevices(
        DiscoveryRecords records,
        string requestedServiceType)
    {
        var normalizedRequestedServiceType = NormalizeDnsName(requestedServiceType);
        var devices = new List<HeraldDiscoveredDevice>();

        foreach (var ptrRecord in records.PtrRecords.Where(record =>
                     record.Name.Equals(normalizedRequestedServiceType, StringComparison.OrdinalIgnoreCase)))
        {
            if (!records.SrvRecords.TryGetValue(ptrRecord.Target, out var srv))
                continue;

            records.TxtRecords.TryGetValue(ptrRecord.Target, out var txt);
            txt ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var host = TrimDotLocal(srv.Host);
            var ipAddress = records.ARecords.TryGetValue(srv.Host, out var resolvedIp)
                ? resolvedIp
                : records.InstanceRemoteAddresses.TryGetValue(ptrRecord.Target, out var remoteIp)
                    ? remoteIp
                    : string.Empty;

            devices.Add(new HeraldDiscoveredDevice
            {
                InstanceName = TrimServiceSuffix(ptrRecord.Target, requestedServiceType),
                Hostname = host,
                IpAddress = ipAddress,
                Port = srv.Port,
                TxtRecords = txt
            });
        }

        return devices;
    }

    private static async Task SendFollowUpQueriesAsync(
        UdpClient udpClient,
        IReadOnlyList<DiscoveryInterface> discoveryInterfaces,
        DiscoveryRecords discoveryRecords,
        HashSet<string> queriedInstances,
        HashSet<string> queriedHosts,
        CancellationToken cancellationToken)
    {
        var instanceQueries = new List<byte[]>();
        var addressQueries = new List<byte[]>();

        foreach (var ptrRecord in discoveryRecords.PtrRecords)
        {
            if (queriedInstances.Add(ptrRecord.Target))
            {
                instanceQueries.Add(BuildSrvQuery(ptrRecord.Target));
                instanceQueries.Add(BuildTxtQuery(ptrRecord.Target));
            }
        }

        foreach (var srvRecord in discoveryRecords.SrvRecords.Values)
        {
            if (queriedHosts.Add(srvRecord.Host))
                addressQueries.Add(BuildAddressQuery(srvRecord.Host));
        }

        foreach (var query in instanceQueries.Concat(addressQueries))
        {
            foreach (var discoveryInterface in discoveryInterfaces)
                await SendQueryAsync(query, discoveryInterface, cancellationToken);
        }
    }

    private static Dictionary<string, string> ReadTxtRecord(byte[] payload)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        while (index < payload.Length)
        {
            var length = payload[index++];
            if (length == 0 || index + length > payload.Length)
                break;

            var entry = Encoding.UTF8.GetString(payload, index, length);
            index += length;

            var separatorIndex = entry.IndexOf('=');
            if (separatorIndex <= 0)
            {
                values[entry] = string.Empty;
                continue;
            }

            values[entry[..separatorIndex]] = entry[(separatorIndex + 1)..];
        }

        return values;
    }

    private static string TrimServiceSuffix(string instanceName, string serviceType)
    {
        var normalizedInstanceName = NormalizeDnsName(instanceName);
        var normalizedServiceType = NormalizeDnsName(serviceType);

        if (normalizedInstanceName.EndsWith($".{normalizedServiceType}", StringComparison.OrdinalIgnoreCase))
            return normalizedInstanceName[..^(normalizedServiceType.Length + 1)];

        return normalizedInstanceName;
    }

    private static string TrimDotLocal(string host)
    {
        var normalizedHost = NormalizeDnsName(host);
        return normalizedHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            ? normalizedHost[..^6]
            : normalizedHost;
    }

    private static string NormalizeDnsName(string name)
    {
        return name.Trim().TrimEnd('.');
    }

    private static void WriteName(BinaryWriter writer, string name)
    {
        foreach (var label in name.Trim('.').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            writer.Write((byte)bytes.Length);
            writer.Write(bytes);
        }

        writer.Write((byte)0);
    }

    private static void WriteUInt16(BinaryWriter writer, ushort value)
    {
        writer.Write(BinaryPrimitives.ReverseEndianness(value));
    }

    private sealed class DnsReader
    {
        private readonly byte[] _buffer;

        public DnsReader(byte[] buffer)
        {
            _buffer = buffer;
        }

        public int Position { get; set; }

        public ushort ReadUInt16()
        {
            var value = BinaryPrimitives.ReadUInt16BigEndian(_buffer.AsSpan(Position, 2));
            Position += 2;
            return value;
        }

        public void SkipUInt16()
        {
            Position += 2;
        }

        public void SkipUInt32()
        {
            Position += 4;
        }

        public void Skip(int length)
        {
            Position += length;
        }

        public byte[] ReadBytes(int length)
        {
            var bytes = _buffer.AsSpan(Position, length).ToArray();
            Position += length;
            return bytes;
        }

        public string ReadName()
        {
            var labels = new List<string>();
            var jumped = false;
            var position = Position;

            while (position < _buffer.Length)
            {
                var length = _buffer[position];
                if (length == 0)
                {
                    position++;
                    if (!jumped)
                        Position = position;
                    break;
                }

                if ((length & 0xC0) == 0xC0)
                {
                    var pointer = ((length & 0x3F) << 8) | _buffer[position + 1];
                    if (!jumped)
                        Position = position + 2;

                    position = pointer;
                    jumped = true;
                    continue;
                }

                position++;
                labels.Add(Encoding.UTF8.GetString(_buffer, position, length));
                position += length;

                if (!jumped)
                    Position = position;
            }

            return string.Join('.', labels);
        }
    }
}
