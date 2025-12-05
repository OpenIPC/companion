using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Companion.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum DeviceType
{
    None,
    Camera,
    Radxa
}
// public enum DeviceType
// {
//     None,
//     Camera,
//     Radxa,
//     NVR
// }


public static class DevicesFriendlyNames
{
    static DevicesFriendlyNames()
    {
        // Keep the values unique too in the dictionaries below as we do reverse searches too in order to build unique firmware filenames
        
        mappingsManufacturers =  new Dictionary<string, string>(){
            {"*", "Generic Manufacturer" },
            {"openipc", "OpenIPC"},
            {"emax", "EMax"},
            {"runcam", "RunCam"},
            {"caddx", "Caddx"}
        };
        mappingsDevices =  new Dictionary<string, string>(){
            {"*", "Generic Device" },
            {"ssc338q", "Generic SSC338Q" },
            {"mario-aio", "OpenIPC Mario AIO"},
            {"thinker-aio", "OpenIPC Thinker AIO"},
            {"thinker-aio-wifi", "OpenIPC Thinker AIO (Built In Wifi)"},
            {"urllc-aio", "OpenIPC Generic SSC338"},
            {"wifilink", "WiFi Link"},
            {"wyvern-link", "Wyvern Link"}
        };

        mappingsFirmwareTypes = new Dictionary<string, string>(){
            {"fpv", "OpenIPC-FPV firmware" },
            {"rubyfpv", "RubyFPV firmware" }
        };
    }

    public static bool FirmwareIsSupported(string firmwareName)
    {
        if (firmwareName.Contains("fpv") || firmwareName.Contains("rubyfpv"))
            return true;
        return false;
    }

    public static string ManufacturerByFriendlyName(string firendlyName)
    {
        foreach (string keyVar in mappingsManufacturers.Keys)
        {
            if (mappingsManufacturers[keyVar] == firendlyName)
            {
                return keyVar;
            }
        }
        return firendlyName;
    }

    public static string DeviceByFriendlyName(string firendlyName)
    {
        foreach (string keyVar in mappingsDevices.Keys)
        {
            if (mappingsDevices[keyVar] == firendlyName)
            {
                return keyVar;
            }
        }
        return firendlyName;
    }

    public static string FirmwareIdByFriendlyName(string firendlyName)
    {
        foreach (string keyVar in mappingsFirmwareTypes.Keys)
        {
            if (mappingsFirmwareTypes[keyVar] == firendlyName)
            {
                return keyVar;
            }
        }
        return firendlyName;
    }

    public static string ManufacturerFriendlyNameById(string manufacturerName)
    {
        if (mappingsManufacturers.ContainsKey(manufacturerName))
            return mappingsManufacturers[manufacturerName];
        return manufacturerName;
    }

    public static string DeviceFriendlyNameById(string deviceName)
    {
        if (mappingsDevices.ContainsKey(deviceName))
            return mappingsDevices[deviceName];
        return deviceName;
    }

    public static string FirmwareFriendlyNameById(string firmwareName)
    {
        if (mappingsFirmwareTypes.ContainsKey(firmwareName))
            return mappingsFirmwareTypes[firmwareName];
        return firmwareName;
    }
    
    public static Dictionary<string, string> mappingsManufacturers;
    public static Dictionary<string, string> mappingsDevices;
    public static Dictionary<string, string> mappingsFirmwareTypes;

}
