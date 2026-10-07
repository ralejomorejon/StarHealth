using Microsoft.UI.Xaml;
using StarHealth.App.ViewModels;
using StarHealth.App.Views;
using StarHealth.Core.Demo;
using StarHealth.Data.Grpc;
using System.IO;

namespace StarHealth.App;

/// <summary>
/// Composition root. One live gRPC client per dish endpoint, always tried
/// first; the shared demo client keeps every screen functional when a dish
/// is unreachable. ViewModels + Core move unchanged into an Uno head.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => CrashLog(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            CrashLog(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
            CrashLog(e.Exception);
    }

    /// <summary>Last-resort crash breadcrumb for unpackaged installs (no debugger
    /// attached out in the field). Never throws.</summary>
    internal static void CrashLog(Exception? ex)
    {
        try
        {
            File.AppendAllText(Path.Combine(LocalData.FolderPath(), "crash.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n---\n");
        }
        catch { }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register(); }
        catch { /* toasts are best-effort */ }

        var storePath = Path.Combine(LocalData.FolderPath(), "history.db");
        var vm = new DashboardViewModel(
            ep => new GrpcDishClient(ep),
            new DemoDishClient(),
            new StarHealth.Data.Store.HistoryStore(storePath));

        _window = new MainWindow(vm);
        _window.Activate();
    }
}
