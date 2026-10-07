using Microsoft.UI.Xaml;
using StarHealth.App.ViewModels;
using StarHealth.App.Views;
using StarHealth.Core.Demo;
using StarHealth.Core.Services;
using StarHealth.Data.Grpc;

namespace StarHealth.App;

/// <summary>
/// Composition root. The live gRPC client is always tried first; the demo
/// client keeps every screen functional when the dish is unreachable.
/// ViewModels + Core move unchanged into an Uno head for other OSes.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private DishPollingService? _poller;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var primary = new GrpcDishClient(new DishEndpointOptions());
        _poller = new DishPollingService(primary, new DemoDishClient());
        var vm = new DashboardViewModel(_poller);

        _window = new MainWindow(vm);
        _window.Activate();
    }
}
