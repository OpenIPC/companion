using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public class SysUpgradeService
{
    private readonly ISshClientService _sshClientService;
    private readonly ILogger _logger;

    public SysUpgradeService(ISshClientService sshClientService, ILogger logger)
    {
        _sshClientService = sshClientService;
        _logger = logger;
    }

    public async Task PerformSysupgradeAsync(DeviceConfig deviceConfig, string kernelPath, string rootfsPath, 
        Action<string> updateProgress, CancellationToken cancellationToken)
    {
        try
        {
            updateProgress("Uploading kernel...");
            string kernelFilename = Path.GetFileName(kernelPath);
            await _sshClientService.UploadFileAsync(deviceConfig, kernelPath, $"{OpenIPC.RemoteTempFolder}/{kernelFilename}");
            updateProgress("Kernel binary uploaded successfully.");

            updateProgress("Uploading root filesystem...");
            string rootfsFilename = Path.GetFileName(rootfsPath);
            await _sshClientService.UploadFileAsync(deviceConfig, rootfsPath, $"{OpenIPC.RemoteTempFolder}/{rootfsFilename}");
            updateProgress("Root filesystem binary uploaded successfully.");

            //updateProgress("Starting sysupgrade...");
            await _sshClientService.ExecuteCommandWithProgressAsync(
                deviceConfig,
                $"sysupgrade --force_ver -n --kernel={OpenIPC.RemoteTempFolder}/{kernelFilename} --rootfs={OpenIPC.RemoteTempFolder}/{rootfsFilename}",
                updateProgress,
                cancellationToken
            );

            updateProgress("Sysupgrade process completed.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during sysupgrade.");
            updateProgress($"Error: {ex.Message}");
        }
    }
}