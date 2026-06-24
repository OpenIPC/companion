using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Serilog;

namespace Companion.Services;

public class SysUpgradeService
{
    private readonly ISshClientService _sshClientService;
    private readonly ILogger _logger;

    // Matches a /proc/mtd line, e.g.   mtd3: 00800000 00010000 "rootfs"
    private static readonly Regex MtdLineRegex = new(
        @"^(?<dev>mtd\d+):\s+(?<size>[0-9a-fA-F]+)\s+(?<erasesize>[0-9a-fA-F]+)\s+""(?<name>[^""]+)""",
        RegexOptions.Compiled);

    public SysUpgradeService(ISshClientService sshClientService, ILogger logger)
    {
        _sshClientService = sshClientService;
        _logger = logger;
    }

    /// <summary>A flash partition parsed from <c>/proc/mtd</c>.</summary>
    public readonly record struct MtdPartition(string Device, long SizeBytes);

    /// <summary>
    /// Parses the output of <c>cat /proc/mtd</c> into a name-&gt;partition map,
    /// e.g. "rootfs" =&gt; { Device = "/dev/mtd3", SizeBytes = 8388608 }.
    /// </summary>
    public static IReadOnlyDictionary<string, MtdPartition> ParseMtdPartitions(string procMtdOutput)
    {
        var map = new Dictionary<string, MtdPartition>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(procMtdOutput))
            return map;

        foreach (var line in procMtdOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = MtdLineRegex.Match(line.Trim());
            if (!match.Success)
                continue;

            if (!long.TryParse(match.Groups["size"].Value, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var size))
                continue;

            var name = match.Groups["name"].Value.Trim();
            map[name] = new MtdPartition($"/dev/{match.Groups["dev"].Value}", size);
        }

        return map;
    }

    public async Task PerformSysupgradeAsync(DeviceConfig deviceConfig, string kernelPath, string rootfsPath, 
        Action<string> updateProgress, CancellationToken cancellationToken)
    {
        try
        {
            string kernelFilename = Path.GetFileName(kernelPath);
            var remoteKernelPath = $"{OpenIPC.RemoteTempFolder}/{kernelFilename}";
            await UploadAndVerifyWithRetryAsync(deviceConfig, kernelPath, remoteKernelPath, "kernel", updateProgress, cancellationToken);
            updateProgress("Kernel binary uploaded successfully.");

            string rootfsFilename = Path.GetFileName(rootfsPath);
            var remoteRootfsPath = $"{OpenIPC.RemoteTempFolder}/{rootfsFilename}";
            await UploadAndVerifyWithRetryAsync(deviceConfig, rootfsPath, remoteRootfsPath, "rootfs", updateProgress, cancellationToken);
            updateProgress("Root filesystem binary uploaded successfully.");

            // sysupgrade loop-mounts the new rootfs to verify it before writing. On a device whose
            // running kernel lacks the squashfs compressor of the new image (commonly XZ), that mount
            // fails with "mount: ... Invalid argument" and sysupgrade aborts *after* it has already
            // flashed the kernel, leaving a half-upgraded unit. Probe the exact same mount first; if the
            // running kernel can't mount the image, skip sysupgrade and write the partitions directly
            // with flashcp (a raw write needs no mount) — the only thing that works on those units.
            if (await CanRunningKernelMountRootfsAsync(deviceConfig, remoteRootfsPath, cancellationToken))
            {
                updateProgress("Starting sysupgrade. Do not unplug the device.");
                await _sshClientService.ExecuteCommandWithProgressAsync(
                    deviceConfig,
                    $"sysupgrade --force_ver -n -z --kernel={OpenIPC.RemoteTempFolder}/{kernelFilename} --rootfs={OpenIPC.RemoteTempFolder}/{rootfsFilename}",
                    updateProgress,
                    cancellationToken,
                    timeout: TimeSpan.FromMinutes(15),
                    allowDisconnectCompletion: true,
                    disableTimeout: true
                );
            }
            else
            {
                updateProgress(
                    "This device's running kernel cannot mount the new root filesystem, so 'sysupgrade' " +
                    "would abort during verification. Flashing the partitions directly instead.");
                await FlashImageDirectlyAsync(
                    deviceConfig, kernelPath, rootfsPath, remoteKernelPath, remoteRootfsPath,
                    updateProgress, cancellationToken);
            }

            await WaitForDeviceRecoveryAsync(deviceConfig, updateProgress, cancellationToken);
            updateProgress("Firmware update completed and device reconnected.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during sysupgrade.");
            updateProgress($"Error: {ex.Message}");
            // Re-throw so the caller does NOT report a successful flash when the
            // upload/verification/flash actually failed. A swallowed exception here
            // is what made a failed flash look identical to a successful one.
            throw;
        }
    }

    private const int UploadMaxAttempts = 3;

    /// <summary>
    /// Uploads a file and verifies it landed at full size on the device, retrying on
    /// failure. SCP uploads over flaky links (e.g. dropbear) can fail or truncate; without
    /// verification the flash would proceed against a missing/partial file. Throws if all
    /// attempts fail so the caller can abort instead of bricking or faking success.
    /// </summary>
    private async Task UploadAndVerifyWithRetryAsync(
        DeviceConfig deviceConfig,
        string localPath,
        string remotePath,
        string label,
        Action<string> updateProgress,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                updateProgress(attempt == 1
                    ? $"Uploading {label}..."
                    : $"Uploading {label}... (attempt {attempt}/{UploadMaxAttempts})");
                await _sshClientService.UploadFileAsync(deviceConfig, localPath, remotePath);
                await ValidateRemoteFileSizeAsync(deviceConfig, localPath, remotePath, label, updateProgress, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < UploadMaxAttempts)
            {
                _logger.Warning(ex, "Upload of {Label} failed on attempt {Attempt}/{Max}; retrying.",
                    label, attempt, UploadMaxAttempts);
                updateProgress($"Upload of {label} failed (attempt {attempt}/{UploadMaxAttempts}); retrying...");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Returns true if the device's currently-running kernel can loop-mount the uploaded rootfs
    /// squashfs. This is exactly what <c>sysupgrade</c> does to verify the image before flashing, so
    /// it predicts whether sysupgrade will succeed or abort with "mount ... Invalid argument".
    /// </summary>
    private async Task<bool> CanRunningKernelMountRootfsAsync(
        DeviceConfig deviceConfig,
        string remoteRootfsPath,
        CancellationToken cancellationToken)
    {
        // Mount read-only via loop, print a sentinel only on success, then always clean up.
        const string sentinel = "RUBY_MOUNT_OK";
        var probe =
            $"d=$(mktemp -d 2>/dev/null || echo /tmp/.cmp_verify); mkdir -p \"$d\"; " +
            $"if mount -t squashfs -o loop,ro '{remoteRootfsPath}' \"$d\" 2>/dev/null; then " +
            $"echo {sentinel}; umount \"$d\" 2>/dev/null; fi; rmdir \"$d\" 2>/dev/null; true";

        try
        {
            var result = await _sshClientService.ExecuteCommandWithResponseAsync(deviceConfig, probe, cancellationToken);
            var ok = result?.Result?.Contains(sentinel, StringComparison.Ordinal) == true;
            _logger.Information("Rootfs mount probe: {Result}.",
                ok ? "mountable (using sysupgrade)" : "NOT mountable (using direct flashcp)");
            return ok;
        }
        catch (Exception ex)
        {
            // If the probe itself can't run, fall back to the existing behaviour (try sysupgrade).
            _logger.Warning(ex, "Rootfs mount probe failed to execute; assuming sysupgrade is usable.");
            return true;
        }
    }

    /// <summary>
    /// Writes the kernel and rootfs straight to their MTD partitions with flashcp (no mount needed),
    /// erases the settings overlay, and reboots — mirroring what <c>sysupgrade --force_ver -n</c>
    /// would have done, for devices where sysupgrade's verify-mount fails. Partitions are looked up by
    /// name from /proc/mtd and size-checked, so we never write the wrong or an oversized partition.
    /// </summary>
    private async Task FlashImageDirectlyAsync(
        DeviceConfig deviceConfig,
        string kernelPath,
        string rootfsPath,
        string remoteKernelPath,
        string remoteRootfsPath,
        Action<string> updateProgress,
        CancellationToken cancellationToken)
    {
        updateProgress("Reading device partition table...");
        var mtdResult = await _sshClientService.ExecuteCommandWithResponseAsync(deviceConfig, "cat /proc/mtd", cancellationToken);
        var partitions = ParseMtdPartitions(mtdResult?.Result ?? string.Empty);

        if (!partitions.TryGetValue("kernel", out var kernelPartition) ||
            !partitions.TryGetValue("rootfs", out var rootfsPartition))
            throw new InvalidOperationException(
                "Could not find 'kernel' and 'rootfs' partitions in /proc/mtd; aborting direct flash to avoid writing the wrong partition.");

        // Refuse to write an image larger than its partition (would corrupt the adjacent partition).
        var kernelSize = new FileInfo(kernelPath).Length;
        var rootfsSize = new FileInfo(rootfsPath).Length;
        if (kernelSize > kernelPartition.SizeBytes)
            throw new InvalidOperationException(
                $"Kernel ({kernelSize} bytes) is larger than its flash partition {kernelPartition.Device} ({kernelPartition.SizeBytes} bytes). Aborting.");
        if (rootfsSize > rootfsPartition.SizeBytes)
            throw new InvalidOperationException(
                $"Root filesystem ({rootfsSize} bytes) is larger than its flash partition {rootfsPartition.Device} ({rootfsPartition.SizeBytes} bytes). Aborting.");

        if (!await RemoteCommandExistsAsync(deviceConfig, "flashcp", cancellationToken))
            throw new InvalidOperationException("'flashcp' (mtd-utils) is not available on the device; cannot flash directly.");

        updateProgress($"Flashing kernel to {kernelPartition.Device}. Do not unplug the device.");
        await _sshClientService.ExecuteCommandWithProgressAsync(
            deviceConfig,
            $"flashcp -v '{remoteKernelPath}' {kernelPartition.Device}",
            updateProgress,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(5),
            disableTimeout: true);

        updateProgress($"Flashing root filesystem to {rootfsPartition.Device}. Do not unplug the device.");
        await _sshClientService.ExecuteCommandWithProgressAsync(
            deviceConfig,
            $"flashcp -v '{remoteRootfsPath}' {rootfsPartition.Device}",
            updateProgress,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(15),
            disableTimeout: true);

        // sysupgrade -n resets the settings overlay; replicate that so stale config from the old
        // firmware doesn't shadow the new image. Best-effort: skip if there is no such partition.
        if (partitions.TryGetValue("rootfs_data", out var overlayPartition))
        {
            updateProgress($"Erasing settings overlay {overlayPartition.Device}...");
            await _sshClientService.ExecuteCommandAsync(deviceConfig, $"flash_eraseall {overlayPartition.Device}");
        }

        updateProgress("Flash complete. Rebooting device. Do not unplug the device.");
        await _sshClientService.ExecuteCommandWithProgressAsync(
            deviceConfig,
            "reboot",
            updateProgress,
            cancellationToken,
            timeout: TimeSpan.FromMinutes(2),
            allowDisconnectCompletion: true,
            disableTimeout: true);
    }

    private async Task<bool> RemoteCommandExistsAsync(
        DeviceConfig deviceConfig,
        string command,
        CancellationToken cancellationToken)
    {
        var result = await _sshClientService.ExecuteCommandWithResponseAsync(
            deviceConfig, $"command -v {command} >/dev/null 2>&1 && echo FOUND", cancellationToken);
        return result?.Result?.Contains("FOUND", StringComparison.Ordinal) == true;
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
