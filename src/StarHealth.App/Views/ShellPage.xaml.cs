using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StarHealth.App.ViewModels;

namespace StarHealth.App.Views;

/// <summary>
/// Desktop shell: left rail copies the app's section list; the shared
/// ViewModel is handed to every page so polling lives once.
/// </summary>
public sealed partial class ShellPage : Page
{
    public DashboardViewModel Vm { get; private set; } = null!;

    public ShellPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardViewModel vm)
        {
            Vm = vm;
            Vm.Navigate = SelectTag;
            Bindings.Update();
        }
        Nav.SelectedItem = Nav.MenuItems[0];
        Go("Estado");
    }

    private void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => Vm.Start();
    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => Vm.Dispose();

    private void OnFrameNavigated(object sender, NavigationEventArgs e) => CenterRoot();
    private void OnFrameSizeChanged(object sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) => CenterRoot();

    /// <summary>
    /// Pins every page's content column to the same centered, capped width.
    /// Star-sized grids need a real width constraint to expand (an unconstrained
    /// centered panel would collapse them), so the shell sets it explicitly:
    /// full width on narrow windows, 1060 centered on wide ones.
    /// </summary>
    private void CenterRoot()
    {
        if (ContentFrame.Content is Microsoft.UI.Xaml.FrameworkElement page &&
            page.FindName("Root") is Microsoft.UI.Xaml.FrameworkElement root)
        {
            root.Width = Math.Max(0, Math.Min(ContentFrame.ActualWidth, 1060));
            root.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center;
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag) Go(tag);
    }

    private void SelectTag(string tag)
    {
        foreach (var m in Nav.MenuItems)
            if (m is NavigationViewItem item && (item.Tag as string) == tag)
            {
                Nav.SelectedItem = item;
                return;
            }
        Go(tag);
    }

    private void Go(string tag)
    {
        var type = tag switch
        {
            "Estadisticas" => typeof(StatsPage),
            "Red" => typeof(NetworkPage),
            "Suscripcion" => typeof(AccountPage),
            "Obstrucciones" => typeof(ObstructionsPage),
            "Alineacion" => typeof(AlignmentPage),
            "Velocidad" => typeof(SpeedPage),
            "Configuracion" => typeof(SettingsPage),
            "Asistencia" => typeof(HelpPage),
            "Acerca" => typeof(AboutPage),
            _ => typeof(StatusPage),
        };
        if (ContentFrame.SourcePageType != type) ContentFrame.Navigate(type, Vm);
    }
}
