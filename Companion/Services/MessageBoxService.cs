using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Companion.Services;
using Serilog;

namespace Companion.Services;

    public class MessageBoxService : IMessageBoxService
    {
        private readonly ILogger _logger;

        public MessageBoxService(ILogger logger)
        {
            _logger = logger;
        }
        
        public async Task ShowMessageBox(string title, string message)
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                title,
                message,
                ButtonEnum.Ok,
                Icon.Info,
                WindowStartupLocation.CenterScreen
            );

            await msgBox.ShowAsync();
        }
        
        public async Task<ButtonResult> ShowMessageBoxWithFolderLink(string title, string message, string filePath)
        {
            // For folder operations, we'll use a custom approach instead
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                title,
                message + "\n\nWould you like to open the containing folder?",
                ButtonEnum.YesNo,
                Icon.Info,
                WindowStartupLocation.CenterScreen
            );

            var result = await msgBox.ShowAsync();
            
            if (result == ButtonResult.Yes)
            {
                try
                {
                    // Get the directory path from the file path
                    string folderPath = Path.GetDirectoryName(filePath);
                    
                    // Open the folder in the default file explorer
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = folderPath,
                            UseShellExecute = true
                        });
                    }
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        // For macOS, properly handle paths with spaces
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = "open",
                            Arguments = $"\"{folderPath}\"",  // Wrap in quotes to handle spaces properly
                            UseShellExecute = true,
                            CreateNoWindow = true
                        };
    
                        Process.Start(startInfo);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "xdg-open",
                            Arguments = folderPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error opening folder");
                    
                    // Show an error message if the folder couldn't be opened
                    await ShowMessageBox("Error", $"Could not open folder: {ex.Message}");
                }
            }
            
            return result;
        }
        
        // A more flexible version that accepts standard button configurations
        public async Task<ButtonResult> ShowCustomMessageBox(string title, string message, ButtonEnum buttons, Icon icon = Icon.Info)
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                title,
                message,
                buttons,
                icon,
                WindowStartupLocation.CenterScreen
            );

            return await msgBox.ShowAsync();
        }
    }
