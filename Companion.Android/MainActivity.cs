using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Companion.Android;
using OpenIPC_Config.Android.Helpers;
using Application = Android.App.Application;

namespace Companion.Android;

[Activity(
    Label = "OpenIPC_Config.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<Companion.App>
{
    // Adjust the layout when the keyboard is shown
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Hide the soft keyboard initially
        Window.SetSoftInputMode(SoftInput.StateHidden);

        // Optionally, adjust the layout when the keyboard is shown
        Window.SetSoftInputMode(SoftInput.AdjustResize);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        AndroidFileHelper.CopyAssetsToInternalStorage(Application.Context);

        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}