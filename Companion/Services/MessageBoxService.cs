using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia.Enums;
using Companion.Services;
using Companion.Views;
using Serilog;

namespace Companion.Services;

    public class MessageBoxService : IMessageBoxService
    {
        private readonly ILogger _logger;

        public MessageBoxService(ILogger logger)
        {
            _logger = logger;
        }
        
        public async Task ShowMessageBox(string title, string message, Window? owner = null, Icon icon = Icon.Info)
        {
            owner ??= ResolveOwnerWindow();
            await ShowSimpleDialogAsync(title, message, ButtonEnum.Ok, owner);
        }
        
        public async Task<ButtonResult> ShowMessageBoxWithFolderLink(string title, string message, string filePath, Window? owner = null)
        {
            var result = await ShowSimpleDialogAsync(
                title,
                message + "\n\nWould you like to open the containing folder?",
                ButtonEnum.YesNo,
                owner);
            
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
                    await ShowMessageBox("Error", $"Could not open folder: {ex.Message}", owner, Icon.Error);
                }
            }
            
            return result;
        }
        
        // A more flexible version that accepts standard button configurations
        public async Task<ButtonResult> ShowCustomMessageBox(string title, string message, ButtonEnum buttons, Icon icon = Icon.Info, Window? owner = null)
        {
            return await ShowSimpleDialogAsync(title, message, buttons, owner);
        }

        private static async Task<ButtonResult> ShowSimpleDialogAsync(string title, string message, ButtonEnum buttons,
            Window? owner = null)
        {
            owner ??= ResolveOwnerWindow();
            var dialog = new SimpleMessageDialog(title, message, buttons);

            if (owner is not null)
                return await dialog.ShowDialog<ButtonResult>(owner);

            dialog.Show();
            return ButtonResult.None;
        }

        private static Window? ResolveOwnerWindow()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            return desktop.Windows.FirstOrDefault(window => window.IsActive)
                   ?? desktop.MainWindow;
        }
    }
