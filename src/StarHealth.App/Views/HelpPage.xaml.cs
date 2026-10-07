using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StarHealth.App.ViewModels;

namespace StarHealth.App.Views;

public sealed partial class HelpPage : Page
{
    public DashboardViewModel Vm { get; private set; } = null!;

    public HelpPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardViewModel vm)
        {
            Vm = vm;
            Bindings.Update();
        }
    }
}
