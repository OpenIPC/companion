using System.Diagnostics;
using Avalonia.Controls;
using Serilog;

namespace Companion.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        var stopwatch = Stopwatch.StartNew();
        InitializeComponent();
        Log.ForContext<MainWindow>().Information("Startup timing: MainWindow.InitializeComponent completed in {ElapsedMs} ms.",
            stopwatch.Elapsed.TotalMilliseconds);
    }
}
