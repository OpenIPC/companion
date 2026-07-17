using Companion.Services;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class SysUpgradeServiceTests
{
    // Real /proc/mtd from an OpenIPC SSC338Q (RunCam WiFiLink) NOR device.
    private const string SampleProcMtd =
        "dev:    size   erasesize  name\n" +
        "mtd0: 00040000 00010000 \"boot\"\n" +
        "mtd1: 00010000 00010000 \"env\"\n" +
        "mtd2: 00200000 00010000 \"kernel\"\n" +
        "mtd3: 00800000 00010000 \"rootfs\"\n" +
        "mtd4: 005b0000 00010000 \"rootfs_data\"\n";

    [Test]
    public void ParseMtdPartitions_MapsNamesToDevicesAndSizes()
    {
        var partitions = SysUpgradeService.ParseMtdPartitions(SampleProcMtd);

        Assert.That(partitions["kernel"].Device, Is.EqualTo("/dev/mtd2"));
        Assert.That(partitions["kernel"].SizeBytes, Is.EqualTo(0x200000));
        Assert.That(partitions["rootfs"].Device, Is.EqualTo("/dev/mtd3"));
        Assert.That(partitions["rootfs"].SizeBytes, Is.EqualTo(0x800000));
        Assert.That(partitions["rootfs_data"].Device, Is.EqualTo("/dev/mtd4"));
        Assert.That(partitions["rootfs_data"].SizeBytes, Is.EqualTo(0x5b0000));
    }

    [Test]
    public void ParseMtdPartitions_LookupIsCaseInsensitive()
    {
        var partitions = SysUpgradeService.ParseMtdPartitions(SampleProcMtd);

        Assert.That(partitions.ContainsKey("ROOTFS"), Is.True);
        Assert.That(partitions.TryGetValue("Kernel", out _), Is.True);
    }

    [Test]
    public void ParseMtdPartitions_EmptyOrGarbage_ReturnsEmpty()
    {
        Assert.That(SysUpgradeService.ParseMtdPartitions(""), Is.Empty);
        Assert.That(SysUpgradeService.ParseMtdPartitions(null!), Is.Empty);
        Assert.That(SysUpgradeService.ParseMtdPartitions("not a partition table\nrandom junk"), Is.Empty);
    }

    [Test]
    public void ParseMtdPartitions_HandlesCrLfAndOtherLayouts()
    {
        var partitions = SysUpgradeService.ParseMtdPartitions("mtd7: 00a00000 00010000 \"rootfs\"\r\n");

        Assert.That(partitions["rootfs"].Device, Is.EqualTo("/dev/mtd7"));
        Assert.That(partitions["rootfs"].SizeBytes, Is.EqualTo(0x00a00000));
    }
}
