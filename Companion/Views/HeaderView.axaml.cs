using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Companion.ViewModels;

namespace Companion.Views;

public partial class HeaderView : UserControl
{
    
    private const string TelegramLink = "https://t.me/+BMyMoolVOpkzNWUy";
    private const string GithubLink = "https://github.com/OpenIPC/";
    //private const string DiscordLink = "https://discord.gg/KtWgDV6Y";
    
    public HeaderView()
    {
        InitializeComponent();

        if (!Design.IsDesignMode) DataContext = App.ServiceProvider.GetService<MainViewModel>();
    }


    private void TelegramButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        _ = launcher?.LaunchUriAsync(new Uri(TelegramLink));
    }

    private void GithubButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        _ = launcher?.LaunchUriAsync(new Uri(GithubLink));
    }

    // private void DiscordButton_OnClick(object? sender, RoutedEventArgs e)
    // {
    //     var launcher = TopLevel.GetTopLevel(this).Launcher;
    //     launcher.LaunchUriAsync(new Uri(DiscordLink));
    // }
}
