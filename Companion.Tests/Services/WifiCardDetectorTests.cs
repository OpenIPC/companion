using Companion.Services;

namespace OpenIPC.Companion.Tests.Services;

[TestFixture]
public class WifiCardDetectorTests
{
    [Test]
    public void DetectWifiCard_Realtek8812_Returns88XXau()
    {
        string lsusbOutput = @"
Bus 001 Device 001: ID 1d6b:0002
Bus 001 Device 002: ID 0bda:8812
"; //The @ allows for a multiline string
        

        string expectedDriver = "88XXau";
        string actualDriver = WifiCardDetector.DetectWifiCard(lsusbOutput); //Or WifiCardDetector.DetectWifiCard(lsusbOutput);

        Assert.AreEqual(expectedDriver, actualDriver, "Driver detection failed for Realtek 8812.");
    }

    [Test]
    public void DetectWifiCard_NoMatchingDevice_ReturnsNull()
    {
        string lsusbOutput = @"
Bus 001 Device 001: ID 1d6b:0002
Bus 001 Device 002: ID 1234:5678
"; //No matching DeviceID
        

        string expectedDriver = null;
        string actualDriver = WifiCardDetector.DetectWifiCard(lsusbOutput); //Or WifiCardDetector.DetectWifiCard(lsusbOutput);

        Assert.AreEqual(expectedDriver, actualDriver, "Driver detection failed for no matching device ID.");
    }

    [Test]
    public void DetectWifiCard_DeviceIdWithLetters_ReturnsCorrectDriver()
    {
        string lsusbOutput = @"
Bus 001 Device 001: ID 1d6b:0002
Bus 001 Device 002: ID 0bda:881a
"; //Device ID with letter

        

        string expectedDriver = "88XXau";
        string actualDriver = WifiCardDetector.DetectWifiCard(lsusbOutput); //Or WifiCardDetector.DetectWifiCard(lsusbOutput);

        Assert.AreEqual(expectedDriver, actualDriver, "Driver detection failed for the correct device id.");
    }
    
    [Test]
    public void DetectWifiCard_DeviceIdBU_ReturnsCorrectDriver()
    {
        string lsusbOutput = @"
Bus 001 Device 001: ID 1d6b:0002
Bus 001 Device 002: ID 0bda:f72b
"; //Device ID with letter

        

        string expectedDriver = "8733bu";
        string actualDriver = WifiCardDetector.DetectWifiCard(lsusbOutput); //Or WifiCardDetector.DetectWifiCard(lsusbOutput);

        Assert.AreEqual(expectedDriver, actualDriver, "Driver detection failed for the correct device id.");
    }
    
    [Test]
    public void DetectWifiCard_DeviceIdEU_ReturnsCorrectDriver()
    {
        string lsusbOutput = @"
Bus 001 Device 001: ID 1d6b:0002
Bus 001 Device 002: ID 0bda:a81a
"; //Device ID with letter

        

        string expectedDriver = "8812eu";
        string actualDriver = WifiCardDetector.DetectWifiCard(lsusbOutput); //Or WifiCardDetector.DetectWifiCard(lsusbOutput);

        Assert.AreEqual(expectedDriver, actualDriver, "Driver detection failed for the correct device id.");
    }
}