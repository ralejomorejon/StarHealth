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
    // Resyncs are deferred through the dispatcher: mutating Items inside a
    // SelectionChanged cascade faults the native control (E_NOTFOUND,
    // instant close, no managed stack).
    private bool _syncDish;
    private bool _pageActive;

    public SettingsPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is DashboardViewModel vm)
        {
            Vm = vm;
            Vm.DishNames.CollectionChanged += OnDishNamesChanged;
            Vm.PropertyChanged += OnVmPropertyChanged;
            _pageActive = true;
            SyncDishBox();
            Bindings.Update();
            LangBox.SelectedIndex = Text.Current == "en" ? 1 : 0;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _pageActive = false;
        if (Vm is not null)
        {
            Vm.DishNames.CollectionChanged -= OnDishNamesChanged;
            Vm.PropertyChanged -= OnVmPropertyChanged;
        }
    }

    private void OnDishNamesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => QueueDishSync();

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.SelectedDishName)) QueueDishSync();
    }

    private void QueueDishSync()
    {
        try { DispatcherQueue.TryEnqueue(SyncDishBox); } catch { SyncDishBox(); }
    }

    private void SyncDishBox()
    {
        if (!_pageActive || Vm is null) return;
        // Do not mutate the combo's Items collection while its dropdown
        // popup is open: the native bridge (CsWinRT) raises E_NOTFOUND
        // (0x80070490) when Items.Clear() runs inside an open popup.
        if (DishBox != null && DishBox.IsDropDownOpen) return;
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
