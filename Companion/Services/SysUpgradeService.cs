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
    
    public async Task PerformBootloaderUpdateAsync(DeviceConfig deviceConfig, string bootloaderPath, 
        Action<string> updateProgress, CancellationToken cancellationToken)
    {
        try
        {
            updateProgress("Uploading bootloader...");
            string bootloaderFilename = Path.GetFileName(bootloaderPath);
            string sentinel = "done";
            await _sshClientService.UploadFileAsync(deviceConfig, bootloaderPath, $"{OpenIPC.RemoteTempFolder}/{bootloaderFilename}");
            updateProgress("Bootloader uploaded successfully.");
            

            //updateProgress("Starting sysupgrade...");
            updateProgress($"Name of uboot file is {bootloaderFilename}");
            await _sshClientService.ExecuteCommandWithProgressAsync(
                deviceConfig,
                "flashcp -v /tmp/u-boot-ssc338q-nor.bin /dev/mtd0; echo \"done\"",
                updateProgress,
                cancellationToken,
                null,
                (output) => output.Trim() == sentinel
            );
            
            await _sshClientService.ExecuteCommandWithProgressAsync(
                deviceConfig,
                "flash_eraseall /dev/mtd1; echo \"done\"",
                updateProgress,
                cancellationToken,
                null,
                (output) => output.Trim() == sentinel
            );

            updateProgress("Bootloader update process completed.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during sysupgrade.");
            updateProgress($"Error: {ex.Message}");
        }
    }
    
    public async Task<string> GetUpdateLinkAsync(DeviceConfig deviceConfig, CancellationToken cancellationToken)
    {
        try
        {
           var res = await _sshClientService.ExecuteCommandWithResponseAsync(
               deviceConfig,
               "fw_printenv -n upgrade",
               cancellationToken);
            return res.Result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error, couldn't get update link.");
            return null;
        }
    }
    
    public async Task<string> GetFlashTypeAsync(DeviceConfig deviceConfig, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _sshClientService.ExecuteCommandWithResponseAsync(
                deviceConfig,
                "ipcinfo -F",
                cancellationToken);
            return res.Result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during sysupgrade.");
            return null;
        }
    }
    
    public async Task<string> GetSOCTypeAsync(DeviceConfig deviceConfig, CancellationToken cancellationToken)
    {
        try
        {
            var res = await _sshClientService.ExecuteCommandWithResponseAsync(
                deviceConfig,
                "fw_printenv -n soc",
                cancellationToken);
            return res.Result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during sysupgrade.");
            return null;
        }
    }
}
