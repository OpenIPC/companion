using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Companion.Models;

namespace Companion.Services;

public class DeviceConfigValidator
{
    private readonly IConfiguration _configuration;
    private readonly Dictionary<DeviceType, List<string>> _deviceHostnameMapping;

    public DeviceConfigValidator(IConfiguration configuration)
    {
        _configuration = configuration;

        _deviceHostnameMapping = _configuration.GetSection("DeviceHostnameMapping")
                                     .Get<Dictionary<DeviceType, List<string>>>()
                                 ?? new Dictionary<DeviceType, List<string>>();
    }

    public bool IsDeviceConfigValid(DeviceConfig deviceConfig)
    {
        if (_deviceHostnameMapping.TryGetValue(deviceConfig.DeviceType, out var allowedHostnames))
            return allowedHostnames.Any(hostname => deviceConfig.Hostname.Contains(hostname));

        return false; // Invalid if no mapping exists
    }
}