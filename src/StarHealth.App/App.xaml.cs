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
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try { Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Register(); }
        catch { /* toasts are best-effort */ }

        var storePath = Path.Combine(
            Windows.Storage.ApplicationData.Current.LocalFolder.Path, "history.db");
        var vm = new DashboardViewModel(
            ep => new GrpcDishClient(ep),
            new DemoDishClient(),
            new StarHealth.Data.Store.HistoryStore(storePath));

        _window = new MainWindow(vm);
        _window.Activate();
    }
}
