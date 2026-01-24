using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MsBox.Avalonia;
using Companion.Models;
using Renci.SshNet;
using Serilog;

namespace Companion.Services;

public class SshClientService : ISshClientService
{
    private IEventSubscriptionService _eventSubscriptionService;

    private readonly ILogger _logger;

    public SshClientService(IEventSubscriptionService eventSubscriptionService, ILogger logger)
    {
        _logger = logger?.ForContext(GetType()) ?? 
                  throw new ArgumentNullException(nameof(logger));
        
        _eventSubscriptionService = eventSubscriptionService;
    }

    public async Task<SshCommand?> ExecuteCommandWithResponseAsync(DeviceConfig deviceConfig, string command,
        CancellationToken cancellationToken = default)
    {
        _logger.Debug($"Executing command: '{command}' on {deviceConfig.IpAddress}.");

        var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
            new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));

        using (var client = new SshClient(connectionInfo))
        {
            try
            {
                await client.ConnectAsync(cancellationToken);
                //client.Connect();
                var result = client.RunCommand(command);
                _logger.Debug($"Command executed successfully. Result: {result.Result}, Exit code: {result.ExitStatus}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Debug($"Error executing command: {ex.Message}");
                return null;
            }
            finally
            {
                client.Disconnect();
            }
        }
    }

    public Task ExecuteCommandAsync(DeviceConfig deviceConfig, string command)
    {
        _logger.Debug($"Executing command: '{command}' on {deviceConfig.IpAddress}.");

        var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
            new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
        using (var client = new SshClient(connectionInfo))
        {
            try
            {
                client.Connect();
                var result = client.RunCommand(command);
                _logger.Information(
                    $"Command executed successfully. Result: {result.Result}, Exit code: {result.ExitStatus}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error executing command: {ex.Message}");
            }
            finally
            {
                client.Disconnect();
            }
        }

        return Task.CompletedTask;
    }


    public async Task UploadFileAsync(DeviceConfig deviceConfig, string localFilePath, string remotePath)
    {
        _logger.Information($"Uploading file '{localFilePath}' to '{remotePath}' on {deviceConfig.IpAddress}.");

        await Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
                new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
            using (var client = new ScpClient(connectionInfo))
            {
                try
                {
                    client.Connect();

                    using (var fileStream = new FileStream(localFilePath, FileMode.Open))
                    {
                        client.Upload(fileStream, remotePath);
                        _logger.Information("File uploaded successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error uploading file: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }
        });
    }

    public void UploadFile(DeviceConfig deviceConfig, string localFilePath, string remotePath)
    {
        _logger.Information($"Uploading file '{localFilePath}' to '{remotePath}' on {deviceConfig.IpAddress}.");

        var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
            new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
        using (var client = new ScpClient(connectionInfo))
        {
            try
            {
                client.Connect();

                using (var fileStream = new FileStream(localFilePath, FileMode.Open))
                {
                    client.Upload(fileStream, remotePath);
                    _logger.Information("File uploaded successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.Information($"Error uploading file: {ex.Message}");
            }
            finally
            {
                client.Disconnect();
            }
        }
    }

    public async Task UploadDirectoryAsync(DeviceConfig deviceConfig, string localDirectory, string remoteDirectory)
    {
        _logger.Information($"Uploading directory '{localDirectory}' to '{remoteDirectory}' on {deviceConfig.IpAddress}.");

        await Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
                new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
            using (var client = new ScpClient(connectionInfo))
            {
                try
                {
                    client.Connect();
                    UploadDirectoryRecursively(client, localDirectory, remoteDirectory);
                    _logger.Information("Directory uploaded successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error uploading directory: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }
        });
    }

    public async Task DownloadDirectoryAsync(DeviceConfig deviceConfig, string remoteDirectory, string localDirectory)
    {
        _logger.Information(
            $"Downloading directory '{remoteDirectory}' to '{localDirectory}' on {deviceConfig.IpAddress}.");

        await Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
                new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
            using (var client = new ScpClient(connectionInfo))
            {
                try
                {
                    client.Connect();
                    DownloadDirectoryRecursively(client, remoteDirectory, localDirectory);
                    _logger.Information("Directory downloaded successfully.");
                }
                catch (Exception ex)
                {
                    _logger.Information($"Error downloading directory: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }
        });
    }

    public async Task<byte[]> DownloadFileBytesAsync(DeviceConfig deviceConfig, string remotePath)
    {
        _logger.Information($"Downloading file from '{remotePath}' on {deviceConfig.IpAddress}.");

        var fileContent = Array.Empty<byte>(); // Initialize an empty byte array

        await Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
                new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
            using (var client = new ScpClient(connectionInfo))
            {
                try
                {
                    client.Connect();
                    using (var memoryStream = new MemoryStream())
                    {
                        // Download the file content into a MemoryStream using ScpClient
                        client.Download(remotePath, memoryStream);
                        fileContent = memoryStream.ToArray(); // Get the file content as a byte array
                        _logger.Information("File downloaded successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error downloading file: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }
        });

        return fileContent; // Return the file content as a byte array
    }


    /// <summary>
    ///     Downloads a file from the device at <paramref name="remotePath" /> and returns its content as a string.
    /// </summary>
    /// <param name="deviceConfig">The device configuration to use for the download.</param>
    /// <param name="remotePath">The path to the file on the device to download.</param>
    /// <returns>The content of the file as a string.</returns>
    public async Task<string> DownloadFileAsync(DeviceConfig deviceConfig, string remotePath)
    {
        _logger.Information($"Downloading file from '{remotePath}' on {deviceConfig.IpAddress}.");

        var fileContent = string.Empty;

        await Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
                new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
            using (var client = new ScpClient(connectionInfo))
            {
                try
                {
                    client.Connect();
                    using (var memoryStream = new MemoryStream())
                    {
                        // Download the file content into a MemoryStream using ScpClient
                        client.Download(remotePath, memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin); // Ensure the stream position is at the beginning
                        fileContent = Encoding.UTF8.GetString(memoryStream.ToArray());
                        _logger.Information("File downloaded successfully.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Information($"Error downloading file: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }
        });

        return fileContent; // Return the content of the file
    }


    public async Task UploadBinaryAsync(DeviceConfig deviceConfig, string remoteDirectory, string fileName)
    {
        await UploadBinaryAsync(deviceConfig, remoteDirectory, OpenIPC.FileType.Normal, fileName);
    }

    public async Task UploadBinaryAsync(DeviceConfig deviceConfig, string remoteDirectory,
        OpenIPC.FileType fileType, string fileName)
    {
        var binariesFolderPath = OpenIPC.GetBinariesPath();
        _logger.Debug($"Binaries folder path: {binariesFolderPath}");
        var filePath = string.Empty;
        var remoteFilePath = string.Empty;

        switch (fileType)
        {
            case OpenIPC.FileType.Normal:
                filePath = Path.Combine(binariesFolderPath, fileName);
                remoteFilePath = Path.Combine(remoteDirectory, fileName);
                break;
            case OpenIPC.FileType.Sensors:
                filePath = Path.Combine(binariesFolderPath, "sensors/", fileName);
                remoteFilePath = Path.Combine(remoteDirectory, fileName);
                break;
            case OpenIPC.FileType.BetaFlightFonts:
                filePath = Path.Combine(binariesFolderPath, "fonts/bf/", fileName);
                remoteFilePath = Path.Combine(remoteDirectory, fileName);
                break;
            case OpenIPC.FileType.iNavFonts:
                filePath = Path.Combine(binariesFolderPath, "fonts/inav/", fileName);
                remoteFilePath = Path.Combine(remoteDirectory, fileName);
                break;
        }

        _logger.Debug($"file path: {filePath}");
        if (File.Exists(filePath))
        {
            _logger.Information($"Uploading {fileName} to {remoteFilePath}...");
            await UploadFileAsync(deviceConfig, filePath, remoteFilePath);
            _logger.Information($"Uploaded {fileName} successfully.");
        }
        else
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard("Error", $"File {fileName} not found.");
            await msgBox.ShowAsync();
        }
    }

    public async Task DownloadFileLocalAsync(DeviceConfig deviceConfig, string remotePath, string localPath)
    {
        try
        {
            var fileContentBytes = await DownloadFileAsync(deviceConfig, remotePath);

            if (fileContentBytes.Length != 0) File.WriteAllText(localPath, fileContentBytes);
        }
        catch (Exception ex)
        {
            _logger.Information($"Error downloading file: {ex.Message}");
        }
    }

    public async Task UploadFileStringAsync(DeviceConfig deviceConfig, string remotePath, string fileContent)
    {
        _logger.Information($"Uploading content to '{remotePath}' on {deviceConfig.IpAddress}.");

        await Task.Run(() =>
        {
            var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
                new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));
            using (var client = new ScpClient(connectionInfo))
            {
                try
                {
                    client.Connect();

                    // Convert the string content to a byte stream
                    using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent)))
                    {
                        // SCP does not directly support uploading from memory streams, so we need to save to a temporary file first
                        var tempFilePath = Path.GetTempFileName();
                        File.WriteAllBytes(tempFilePath, memoryStream.ToArray());

                        // Upload the temporary file
                        client.Upload(new FileInfo(tempFilePath), remotePath);
                        _logger.Information("File uploaded successfully.");

                        // Delete the temporary file
                        File.Delete(tempFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Information($"Error uploading file: {ex.Message}");
                }
                finally
                {
                    client.Disconnect();
                }
            }
        });
    }

    // This is used for sysupgrade commands
    public async Task ExecuteCommandWithProgressAsync(
        DeviceConfig deviceConfig,
        string command,
        Action<string> updateProgress,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        Func<string, bool>? isCommandComplete = null)
    {
        timeout ??= TimeSpan.FromMinutes(2); // Default timeout
        isCommandComplete ??= output => output.Contains("Unconditional reboot") || output.Contains("Arguments written");

        var connectionInfo = new ConnectionInfo(deviceConfig.IpAddress, deviceConfig.Port, deviceConfig.Username,
            new PasswordAuthenticationMethod(deviceConfig.Username, deviceConfig.Password));

        using var client = new SshClient(connectionInfo);
        try
        {
            client.Connect();
            if (!client.IsConnected)
            {
                updateProgress("Unable to connect to the host.");
                return;
            }

            using var shellStream = client.CreateShellStream("commands", 80, 24, 800, 600, 1024);
            shellStream.WriteLine(command);

            var startTime = DateTime.UtcNow;
            var lastUpdateTime = DateTime.UtcNow;
            var commandCompleted = false;

            // Read output continuously
            while (!cancellationToken.IsCancellationRequested && !commandCompleted)
            {
                if (!client.IsConnected)
                {
                    updateProgress("Connection lost.");
                    break;
                }

                if (DateTime.UtcNow - startTime > timeout)
                {
                    updateProgress("Command execution timed out.");
                    break;
                }

                // Read available data
                while (shellStream.DataAvailable)
                {
                    var output = shellStream.ReadLine();
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        // Send progress updates directly
                        updateProgress(output);

                        // Check if the command is complete
                        if (isCommandComplete(output))
                        {
                            commandCompleted = true;
                            updateProgress("Command execution completed.");
                            break;
                        }
                    }
                }

                // Rate-limit updates to prevent tight loop (every 50ms)
                await Task.Delay(50, cancellationToken);
            }

            // Final status if the command did not complete successfully
            if (!commandCompleted)
            {
                updateProgress("Command did not complete successfully.");
            }
        }
        catch (OperationCanceledException)
        {
            updateProgress("Operation canceled by user.");
        }
        catch (Exception ex)
        {
            updateProgress($"Error: {ex.Message}");
        }
        finally
        {
            client.Disconnect();
        }
    }


    public void UploadDirectoryRecursively(ScpClient client, string localDirectory, string remoteDirectory)
    {
        using (var sshClient = new SshClient(client.ConnectionInfo))
        {
            sshClient.Connect();
            sshClient.RunCommand($"mkdir -p {remoteDirectory}");
            sshClient.Disconnect();
        }

        var files = Directory.GetFiles(localDirectory);
        foreach (var file in files)
        {
            var remoteFilePath = Path.Combine(remoteDirectory, Path.GetFileName(file)).Replace("\\", "/");
            _logger.Information($"Uploading file {file} to {remoteFilePath}");
            using (var fileStream = new FileStream(file, FileMode.Open))
            {
                client.Upload(fileStream, remoteFilePath);
            }
        }

        var directories = Directory.GetDirectories(localDirectory);
        foreach (var directory in directories)
        {
            var remoteSubDirectory = Path.Combine(remoteDirectory, Path.GetFileName(directory)).Replace("\\", "/");
            UploadDirectoryRecursively(client, directory, remoteSubDirectory);
        }
    }

    public void DownloadDirectoryRecursively(ScpClient client, string remoteDirectory, string localDirectory)
    {
        if (!Directory.Exists(localDirectory)) Directory.CreateDirectory(localDirectory);

        using (var sshClient = new SshClient(client.ConnectionInfo))
        {
            sshClient.Connect();
            var command = sshClient.RunCommand($"ls -F {remoteDirectory}");
            sshClient.Disconnect();

            var files = command.Result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var file in files)
                if (file.EndsWith("/"))
                {
                    var remoteSubDirectory = $"{remoteDirectory}/{file.TrimEnd('/')}";
                    var localSubDirectory = Path.Combine(localDirectory, file.TrimEnd('/'));
                    DownloadDirectoryRecursively(client, remoteSubDirectory, localSubDirectory);
                }
                else
                {
                    var remoteFilePath = $"{remoteDirectory}/{file}";
                    var localFilePath = Path.Combine(localDirectory, file);
                    _logger.Information($"Downloading file {remoteFilePath} to {localFilePath}");

                    using (var fileStream = new FileStream(localFilePath, FileMode.Create))
                    {
                        client.Download(remoteFilePath, fileStream);
                    }
                }
        }
    }
}
