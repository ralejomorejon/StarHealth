using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StarHealth.App.ViewModels;

namespace StarHealth.App.Views;

public sealed partial class NetworkPage : Page, INotifyPropertyChanged
{
    public DashboardViewModel Vm { get; private set; } = null!;

    private string _routerResult = StarHealth.Core.Localization.Text.Get("net.router.pending");
    public string RouterResult
    {
        get => _routerResult;
        set { _routerResult = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public NetworkPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardViewModel vm)
        {
            Vm = vm;
            Bindings.Update();
        }
    }

    private async void OnCheckRouter(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RouterResult = StarHealth.Core.Localization.Text.Get("net.router.checking");
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await tcp.ConnectAsync("192.168.1.1", 9000, cts.Token).ConfigureAwait(false);
            DispatcherQueue.TryEnqueue(() => RouterResult =
                StarHealth.Core.Localization.Text.Get("net.router.ok"));
        }
        catch (Exception)
        {
            DispatcherQueue.TryEnqueue(() => RouterResult =
                StarHealth.Core.Localization.Text.Get("net.router.fail"));
        }
    }
}
