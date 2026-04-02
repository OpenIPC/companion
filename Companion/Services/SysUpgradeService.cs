using System;
using System.IO;
using System.Net.NetworkInformation;
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

            updateProgress("Starting sysupgrade. Do not unplug the device.");
            await _sshClientService.ExecuteCommandWithProgressAsync(
                deviceConfig,
                $"sysupgrade --force_ver -n --kernel={OpenIPC.RemoteTempFolder}/{kernelFilename} --rootfs={OpenIPC.RemoteTempFolder}/{rootfsFilename}",
                updateProgress,
                cancellationToken,
                timeout: TimeSpan.FromMinutes(15),
                allowDisconnectCompletion: true,
                disableTimeout: true
            );

            await WaitForDeviceRecoveryAsync(deviceConfig, updateProgress, cancellationToken);
            updateProgress("Sysupgrade process completed and device reconnected.");
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

    private async Task WaitForDeviceRecoveryAsync(
        DeviceConfig deviceConfig,
        Action<string> updateProgress,
        CancellationToken cancellationToken)
    {
        updateProgress("Waiting for device reboot. Connection loss is expected. Do not unplug the device.");

        bool sawOffline = await WaitForPingStateAsync(deviceConfig.IpAddress, expectedOnline: false,
            TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(2), cancellationToken,
            percent => updateProgress($"Recovery progress: offline {percent}%"));

        if (sawOffline)
            updateProgress("Device went offline. Waiting for it to come back...");
        else
            updateProgress("Did not observe disconnect. Waiting for device to become reachable...");

        bool pingRecovered = await WaitForPingStateAsync(deviceConfig.IpAddress, expectedOnline: true,
            TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(3), cancellationToken,
            percent => updateProgress($"Recovery progress: ping {percent}%"));

        if (!pingRecovered)
            throw new InvalidOperationException(
                "Unable to verify completion. Device did not return within the recovery window. Do not unplug power yet.");

        updateProgress("Device is reachable again. Waiting for SSH...");

        bool sshRecovered = await WaitForSshAsync(deviceConfig, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3),
            cancellationToken,
            percent => updateProgress($"Recovery progress: ssh {percent}%"));

        if (!sshRecovered)
            throw new InvalidOperationException(
                "Device responded to ping but SSH did not become ready in time. Do not unplug power yet.");

        updateProgress("Device reconnected successfully.");
    }

    private async Task<bool> WaitForPingStateAsync(
        string ipAddress,
        bool expectedOnline,
        TimeSpan timeout,
        TimeSpan interval,
        CancellationToken cancellationToken,
        Action<int>? reportProgress = null)
    {
        using var ping = new Ping();
        var deadline = DateTime.UtcNow + timeout;
        var startTime = DateTime.UtcNow;
        int lastReportedProgress = -1;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            ReportRecoveryProgress(startTime, timeout, reportProgress, ref lastReportedProgress);

            try
            {
                var reply = await ping.SendPingAsync(ipAddress, 1000);
                bool isOnline = reply.Status == IPStatus.Success;
                if (isOnline == expectedOnline)
                {
                    reportProgress?.Invoke(100);
                    return true;
                }
            }
            catch
            {
                if (!expectedOnline)
                {
                    reportProgress?.Invoke(100);
                    return true;
                }
            }

            await Task.Delay(interval, cancellationToken);
        }

        return false;
    }

    private async Task<bool> WaitForSshAsync(
        DeviceConfig deviceConfig,
        TimeSpan timeout,
        TimeSpan interval,
        CancellationToken cancellationToken,
        Action<int>? reportProgress = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        var startTime = DateTime.UtcNow;
        int lastReportedProgress = -1;

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            ReportRecoveryProgress(startTime, timeout, reportProgress, ref lastReportedProgress);

            var commandResult = await _sshClientService.ExecuteCommandWithResponseAsync(
                deviceConfig,
                "echo ready",
                cancellationToken);

            if (commandResult != null && commandResult.ExitStatus == 0 &&
                commandResult.Result.Trim().Equals("ready", StringComparison.OrdinalIgnoreCase))
            {
                reportProgress?.Invoke(100);
                return true;
            }

            await Task.Delay(interval, cancellationToken);
        }

        return false;
    }

    private static void ReportRecoveryProgress(
        DateTime startTime,
        TimeSpan timeout,
        Action<int>? reportProgress,
        ref int lastReportedProgress)
    {
        if (reportProgress is null || timeout <= TimeSpan.Zero)
            return;

        var elapsed = DateTime.UtcNow - startTime;
        var percent = (int)Math.Clamp(Math.Round(elapsed.TotalMilliseconds / timeout.TotalMilliseconds * 100.0), 0, 99);
        if (percent == lastReportedProgress)
            return;

        lastReportedProgress = percent;
        reportProgress(percent);
    }
}
