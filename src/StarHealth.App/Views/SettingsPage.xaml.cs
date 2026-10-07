using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StarHealth.App.ViewModels;
using StarHealth.Core.Localization;

namespace StarHealth.App.Views;

public sealed partial class SettingsPage : Page
{
    public DashboardViewModel Vm { get; private set; } = null!;

    // The dish switcher is populated imperatively: binding an
    // ObservableCollection to ComboBox.ItemsSource crashes the ABI bridge
    // (CsWinRT vtable lookup references a phantom ComInterfaceEntry).
    // ComboBoxItems created here are pure WinRT types, which marshal fine.
    private bool _syncDish;

    public SettingsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardViewModel vm)
        {
            Vm = vm;
            Vm.DishNames.CollectionChanged += OnDishNamesChanged;
            Vm.PropertyChanged += OnVmPropertyChanged;
            SyncDishBox();
            Bindings.Update();
            LangBox.SelectedIndex = Text.Current == "en" ? 1 : 0;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (Vm is not null)
        {
            Vm.DishNames.CollectionChanged -= OnDishNamesChanged;
            Vm.PropertyChanged -= OnVmPropertyChanged;
        }
    }

    private void OnDishNamesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => SyncDishBox();

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.SelectedDishName)) SyncDishBox();
    }

    private void SyncDishBox()
    {
        if (Vm is null) return;
        _syncDish = true;
        try
        {
            DishBox.Items.Clear();
            foreach (var n in Vm.DishNames) DishBox.Items.Add(new ComboBoxItem { Content = n });
            DishBox.SelectedItem = DishBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(i => (string?)i.Content == Vm.SelectedDishName);
        }
        finally { _syncDish = false; }
    }

    private void OnDishSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncDish || Vm is null) return;
        if (DishBox.SelectedItem is ComboBoxItem { Content: string name }) Vm.SelectedDishName = name;
    }

    private void OnLangChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Vm is null || LangBox.SelectedItem is not ComboBoxItem item) return;
        Vm.ChangeLanguage((string)item.Tag);
    }
}
