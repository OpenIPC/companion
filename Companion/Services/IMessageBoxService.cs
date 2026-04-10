using System.Threading.Tasks;
using Avalonia.Controls;
using MsBox.Avalonia.Enums;

namespace Companion.Services;

public interface IMessageBoxService
{
    Task ShowMessageBox(string title, string message, Window? owner = null, Icon icon = Icon.Info);
    Task<ButtonResult> ShowMessageBoxWithFolderLink(string title, string message, string filePath, Window? owner = null);
    Task<ButtonResult> ShowCustomMessageBox(string title, string message, ButtonEnum buttons, Icon icon = Icon.Info, Window? owner = null);
}
