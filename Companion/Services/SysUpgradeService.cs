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
    /// Seconds the device is given to complete the verify-mount before we stop waiting on it.
    /// </summary>
    private const int MountProbeSeconds = 45;

    /// <summary>
    /// Returns true if the device's currently-running kernel can loop-mount the uploaded rootfs
    /// squashfs. This is exactly what <c>sysupgrade</c> does to verify the image before flashing, so
    /// it predicts whether sysupgrade will succeed or abort with "mount ... Invalid argument".
    /// </summary>
    /// <remarks>
    /// The mount does not always fail cleanly — it can <b>block indefinitely</b>. Observed on an
    /// SSC338Q air unit: sysupgrade printed "Update rootfs from /tmp/rootfs.squashfs.ssc338q" and
    /// never emitted another byte, because everything between that line and the flash write is a
    /// silent losetup+mount. An unbounded probe would therefore hang in exactly the case this
    /// fallback exists to survive, so it is bounded twice, and a probe that does not answer in time
    /// counts as NOT mountable — handing such a device to sysupgrade would only wedge it on the very
    /// same mount.
    /// </remarks>
    private async Task<bool> CanRunningKernelMountRootfsAsync(
        DeviceConfig deviceConfig,
        string remoteRootfsPath,
        CancellationToken cancellationToken)
    {
        // Mount read-only via loop, print a sentinel only on success, then always clean up.
        // `timeout` keeps the device-side mount from lingering forever; it is best-effort only,
        // since the SIGTERM it sends cannot free a mount wedged in uninterruptible (D) state, and
        // the applet may be absent on a minimal build. Hence the client-side bound below as well.
        //
        // The bare, un-timed mount is used ONLY when the `timeout` applet is missing — never as a
        // fallback after a mount failure/timeout. `M` holds `timeout N` when the applet exists and is
        // empty otherwise, so exactly one mount runs: a bounded one where possible. A `... || mount`
        // form would spawn a second unbounded mount on every real mount failure, which — because the
        // client-side wait below abandons the command without killing it — is precisely the
        // background-wedge this probe exists to avoid.
        const string sentinel = "RUBY_MOUNT_OK";
        var probe =
            $"d=$(mktemp -d 2>/dev/null || echo /tmp/.cmp_verify); mkdir -p \"$d\"; " +
            $"if command -v timeout >/dev/null 2>&1; then M=\"timeout {MountProbeSeconds}\"; else M=\"\"; fi; " +
            $"if $M mount -t squashfs -o loop,ro '{remoteRootfsPath}' \"$d\" 2>/dev/null; then " +
            $"echo {sentinel}; umount \"$d\" 2>/dev/null; fi; rmdir \"$d\" 2>/dev/null; true";

        try
        {
            // A CancellationToken cannot rescue us here: ExecuteCommandWithResponseAsync runs the
            // blocking SSH.NET RunCommand inside Task.Run, whose token only prevents the delegate
            // from *starting* — once it is running, cancelling it does nothing and the await would
            // wait forever. Bound it on the wall clock instead.
            var probeTask = _sshClientService.ExecuteCommandWithResponseAsync(deviceConfig, probe, cancellationToken);
            var limit = Task.Delay(TimeSpan.FromSeconds(MountProbeSeconds + 20), cancellationToken);

            if (await Task.WhenAny(probeTask, limit) != probeTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _logger.Warning(
                    "Rootfs mount probe did not answer within {Seconds}s — the mount is wedged. " +
                    "Treating rootfs as NOT mountable (using direct flashcp).",
                    MountProbeSeconds + 20);
                return false;
            }

            var result = await probeTask;
            var ok = result?.Result?.Contains(sentinel, StringComparison.Ordinal) == true;
            _logger.Information("Rootfs mount probe: {Result}.",
                ok ? "mountable (using sysupgrade)" : "NOT mountable (using direct flashcp)");
            return ok;
        }
        catch (OperationCanceledException)
        {
            throw;
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
        await FlashPartitionAsync(
            deviceConfig, remoteKernelPath, kernelPartition.Device, "kernel",
            TimeSpan.FromMinutes(5), updateProgress, cancellationToken);

        updateProgress($"Flashing root filesystem to {rootfsPartition.Device}. Do not unplug the device.");
        await FlashPartitionAsync(
            deviceConfig, remoteRootfsPath, rootfsPartition.Device, "root filesystem",
            TimeSpan.FromMinutes(15), updateProgress, cancellationToken);

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

    /// <summary>
    /// Writes one image to one MTD partition with <c>flashcp -v</c>, blocking until the write reports
    /// an explicit exit code, and throwing if it failed or never completed.
    /// </summary>
    /// <remarks>
    /// <see cref="ISshClientService.ExecuteCommandWithProgressAsync"/> decides a command is finished
    /// only when it prints a sysupgrade-style sentinel or the SSH session drops. flashcp does neither —
    /// it streams progress and returns to the shell prompt — so with <c>disableTimeout</c> the read loop
    /// would spin forever, and even on timeout the helper only reports via progress text without
    /// throwing. Raw MTD writes must fail loudly, so we append <c>; echo MARKER$?</c> to carry flashcp's
    /// exit status back over the shell stream, complete the moment that marker is parsed, keep a finite
    /// timeout, and throw on a non-zero code or on no marker at all (timeout / lost connection).
    /// </remarks>
    private async Task FlashPartitionAsync(
        DeviceConfig deviceConfig,
        string remoteImagePath,
        string partitionDevice,
        string label,
        TimeSpan timeout,
        Action<string> updateProgress,
        CancellationToken cancellationToken)
    {
        const string marker = "RUBY_FLASHCP_EXIT:";
        int? exitCode = null;

        // Parse the "MARKER<code>" line flashcp's shell prints on exit. The shell also echoes the
        // command itself, which contains the literal "MARKER$?" (no digits) — skipping the no-digit
        // case keeps that echo from being mistaken for completion.
        void Sniff(string line)
        {
            updateProgress(line);
            var idx = line.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
                return;
            var end = idx + marker.Length;
            while (end < line.Length && char.IsWhiteSpace(line[end]))
                end++;
            var start = end;
            while (end < line.Length && char.IsDigit(line[end]))
                end++;
            if (end > start && int.TryParse(line.Substring(start, end - start), out var code))
                exitCode = code;
        }

        await _sshClientService.ExecuteCommandWithProgressAsync(
            deviceConfig,
            $"flashcp -v '{remoteImagePath}' {partitionDevice}; echo {marker}$?",
            Sniff,
            cancellationToken,
            timeout: timeout,
            // Sniff runs before this check each line, so "we parsed a real exit code" is completion.
            isCommandComplete: _ => exitCode.HasValue,
            disableTimeout: false);

        if (exitCode is null)
            throw new InvalidOperationException(
                $"Flashing {label} to {partitionDevice} did not report completion within {timeout.TotalMinutes:0} min " +
                "(flashcp may be wedged or the connection dropped); aborting to avoid a silently half-written flash.");
        if (exitCode != 0)
            throw new InvalidOperationException(
                $"flashcp failed writing {label} to {partitionDevice} (exit {exitCode}); the flash is likely incomplete.");
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
