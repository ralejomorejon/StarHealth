using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StarHealth.App.ViewModels;
using StarHealth.Core.Localization;

namespace StarHealth.App.Views;

public sealed partial class SettingsPage : Page
{
    public DashboardViewModel Vm { get; private set; } = null!;

    public SettingsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardViewModel vm)
        {
            Vm = vm;
            Bindings.Update();
            LangBox.SelectedIndex = Text.Current == "en" ? 1 : 0;
        }
    }

    private void OnLangChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || LangBox.SelectedItem is not ComboBoxItem item) return;
        Vm.ChangeLanguage((string)item.Tag);
    }
}
