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
            var remoteKernelPath = $"{OpenIPC.RemoteTempFolder}/{kernelFilename}";
            await _sshClientService.UploadFileAsync(deviceConfig, kernelPath, remoteKernelPath);
            await ValidateRemoteFileSizeAsync(deviceConfig, kernelPath, remoteKernelPath, "kernel", updateProgress, cancellationToken);
            updateProgress("Kernel binary uploaded successfully.");

            updateProgress("Uploading root filesystem...");
            string rootfsFilename = Path.GetFileName(rootfsPath);
            var remoteRootfsPath = $"{OpenIPC.RemoteTempFolder}/{rootfsFilename}";
            await _sshClientService.UploadFileAsync(deviceConfig, rootfsPath, remoteRootfsPath);
            await ValidateRemoteFileSizeAsync(deviceConfig, rootfsPath, remoteRootfsPath, "rootfs", updateProgress, cancellationToken);
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

    private async Task ValidateRemoteFileSizeAsync(
        DeviceConfig deviceConfig,
        string localPath,
        string remotePath,
        string label,
        Action<string> updateProgress,
        CancellationToken cancellationToken)
    {
        var localSize = new FileInfo(localPath).Length;
        updateProgress($"Validating {label} upload...");

        var result = await _sshClientService.ExecuteCommandWithResponseAsync(
            deviceConfig,
            $"wc -c < {remotePath}",
            cancellationToken);

        if (result == null || string.IsNullOrWhiteSpace(result.Result))
            throw new InvalidOperationException($"Failed to verify {label} upload (no response).");

        if (!long.TryParse(result.Result.Trim(), out var remoteSize))
            throw new InvalidOperationException($"Failed to parse {label} size from device.");

        if (remoteSize != localSize)
            throw new InvalidOperationException($"{label} upload size mismatch. Local={localSize} Remote={remoteSize}");
    }
}
