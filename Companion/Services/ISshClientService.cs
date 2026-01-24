using System;
using System.Threading;
using System.Threading.Tasks;
using Companion.Models;
using Renci.SshNet;

namespace Companion.Services;

public interface ISshClientService
{
    // Executes a command on the remote device and returns its response
    Task<SshCommand?> ExecuteCommandWithResponseAsync(DeviceConfig deviceConfig, string command,
        CancellationToken cancellationToken);

    
    // Executes a command on the remote device asynchronously
    Task ExecuteCommandAsync(DeviceConfig deviceConfig, string command);

    // Uploads a file from a local path to a remote path asynchronously using SCP
    Task UploadFileAsync(DeviceConfig deviceConfig, string localFilePath, string remotePath);

    // Synchronously uploads a file from a local path to a remote path using SCP
    void UploadFile(DeviceConfig deviceConfig, string localFilePath, string remotePath);

    // Uploads string content to a file on the remote device asynchronously using SCP
    Task UploadFileStringAsync(DeviceConfig deviceConfig, string remotePath, string fileContent);

    // Downloads a file from the remote device and returns its content as a string
    Task<byte[]> DownloadFileBytesAsync(DeviceConfig deviceConfig, string remotePath);

    Task<string> DownloadFileAsync(DeviceConfig deviceConfig, string remotePath);

    // Downloads a file from the remote path to the local path asynchronously using SCP
    Task DownloadFileLocalAsync(DeviceConfig deviceConfig, string remotePath, string localPath);

    // Recursively downloads all files and directories from a remote directory to a local directory
    Task DownloadDirectoryAsync(DeviceConfig deviceConfig, string remoteDirectory, string localDirectory);

    // Recursively uploads all files and directories from a local directory to a remote directory asynchronously using SCP
    Task UploadDirectoryAsync(DeviceConfig deviceConfig, string localDirectory, string remoteDirectory);

    // Uploads a specific binary file by file name using SCP
    Task UploadBinaryAsync(DeviceConfig deviceConfig, string remoteDirectory, string fileName);

    // Uploads a specific binary file by file type and name using SCP
    Task UploadBinaryAsync(DeviceConfig deviceConfig, string remoteDirectory, OpenIPC.FileType fileType,
        string fileName);

    // Executes a command on the remote device asynchronously with progress updates
    // Task ExecuteCommandWithProgressAsync(DeviceConfig deviceConfig, string command,
    //     Action<string> updateProgress, CancellationToken cancellationToken = default);

    Task ExecuteCommandWithProgressAsync(
        DeviceConfig deviceConfig,
        string command,
        Action<string> updateProgress,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Func<string, bool>? isCommandComplete = null);
}
