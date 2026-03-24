using System.Net;
using System.Text;
using Companion.Models;
using Companion.Services;

namespace OpenIPC.Companion.Tests.Services;

public class HeraldDiscoveryServiceTests
{
    [Test]
    public void ParseResponse_ExtractsHeraldDevice()
    {
        var packet = BuildMdnsResponsePacket();
        var remote = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353);

        var devices = HeraldDiscoveryService.ParseResponse(
            packet,
            remote,
            HeraldDiscoveredDevice.DefaultServiceType);

        Assert.That(devices, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(devices[0].InstanceName, Is.EqualTo("testcam"));
            Assert.That(devices[0].Hostname, Is.EqualTo("testcam"));
            Assert.That(devices[0].IpAddress, Is.EqualTo("192.168.1.50"));
            Assert.That(devices[0].SshPort, Is.EqualTo(22));
            Assert.That(devices[0].Vendor, Is.EqualTo("OpenIPC"));
            Assert.That(devices[0].Model, Is.EqualTo("general"));
            Assert.That(devices[0].DeviceType, Is.EqualTo(DeviceType.Camera));
            Assert.That(devices[0].DisplayName, Does.Contain("general"));
        });
    }

    [Test]
    public void ParseResponse_HandlesTrailingDotsInDnsNames()
    {
        var packet = BuildMdnsResponsePacket(
            "_ssh._tcp.local.",
            "testcam._ssh._tcp.local.",
            "testcam.local.");
        var remote = new IPEndPoint(IPAddress.Parse("192.168.1.50"), 5353);

        var devices = HeraldDiscoveryService.ParseResponse(
            packet,
            remote,
            HeraldDiscoveredDevice.DefaultServiceType);

        Assert.That(devices, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(devices[0].InstanceName, Is.EqualTo("testcam"));
            Assert.That(devices[0].Hostname, Is.EqualTo("testcam"));
            Assert.That(devices[0].IpAddress, Is.EqualTo("192.168.1.50"));
        });
    }

    private static byte[] BuildMdnsResponsePacket(
        string? serviceType = null,
        string? instanceName = null,
        string? hostName = null)
    {
        serviceType ??= HeraldDiscoveredDevice.DefaultServiceType;
        instanceName ??= $"testcam.{serviceType}";
        hostName ??= "testcam.local";

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        WriteUInt16(writer, 0);
        WriteUInt16(writer, 0x8400);
        WriteUInt16(writer, 0);
        WriteUInt16(writer, 4);
        WriteUInt16(writer, 0);
        WriteUInt16(writer, 0);

        WritePtrRecord(writer,
            serviceType,
            instanceName);
        WriteSrvRecord(writer,
            instanceName,
            hostName,
            22);
        WriteTxtRecord(writer,
            instanceName,
            new[]
            {
                "vendor=OpenIPC",
                "model=general"
            });
        WriteARecord(writer, hostName, IPAddress.Parse("192.168.1.50"));

        return stream.ToArray();
    }

    private static void WritePtrRecord(BinaryWriter writer, string name, string target)
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

    private static void WriteSrvRecord(BinaryWriter writer, string name, string target, ushort port)
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
        WriteName(rdataWriter, target);

        WriteUInt16(writer, (ushort)rdata.Length);
        writer.Write(rdata.ToArray());
    }

    private static void WriteTxtRecord(BinaryWriter writer, string name, IEnumerable<string> entries)
    {
        WriteName(writer, name);
        WriteUInt16(writer, 16);
        WriteUInt16(writer, 1);
        WriteUInt32(writer, 120);

        var payload = entries
            .SelectMany(entry =>
            {
                var bytes = Encoding.UTF8.GetBytes(entry);
                return new[] { (byte)bytes.Length }.Concat(bytes);
            })
            .ToArray();

        WriteUInt16(writer, (ushort)payload.Length);
        writer.Write(payload);
    }

    private static void WriteARecord(BinaryWriter writer, string name, IPAddress address)
    {
        WriteName(writer, name);
        WriteUInt16(writer, 1);
        WriteUInt16(writer, 1);
        WriteUInt32(writer, 120);
        WriteUInt16(writer, 4);
        writer.Write(address.GetAddressBytes());
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
        writer.Write((byte)(value >> 8));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteUInt32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }
}
