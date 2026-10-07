using Microsoft.UI.Xaml;
using StarHealth.App.ViewModels;
using StarHealth.App.Views;
using StarHealth.Core.Demo;
using StarHealth.Data.Grpc;

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
        var vm = new DashboardViewModel(
            ep => new GrpcDishClient(ep),
            new DemoDishClient());

        _window = new MainWindow(vm);
        _window.Activate();
    }
}
