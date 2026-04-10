using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Companion.Views;

public partial class StartupSplashWindow : Window, INotifyPropertyChanged
{
    public string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string VersionText { get; } = global::Companion.Services.VersionHelper.GetAppVersion();

    private string _statusText = "Starting...";

    public new event PropertyChangedEventHandler? PropertyChanged;

    public StartupSplashWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
