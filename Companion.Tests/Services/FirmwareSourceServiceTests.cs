using Companion.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Serilog;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class FirmwareSourceServiceTests
{
    [Test]
    public void GetSources_LegacyTestTrack_ReadsAsExperimental()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FirmwareSources:0:Name"] = "Mabur",
                ["FirmwareSources:0:Track"] = "Test",
                ["FirmwareSources:0:Channels:0:Name"] = "Latest",
                ["FirmwareSources:0:Channels:0:Owner"] = "gilankpam",
                ["FirmwareSources:0:Channels:0:Repository"] = "openipc-builder"
            })
            .Build();
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.ForContext<FirmwareSourceService>()).Returns(logger.Object);

        var sources = new FirmwareSourceService(configuration, logger.Object).GetSources();

        Assert.That(sources.Single().Track, Is.EqualTo("Experimental"));
    }
}
