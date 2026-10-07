using Microsoft.UI.Xaml;
using StarHealth.App.ViewModels;
using StarHealth.App.Views;

namespace StarHealth.App;

/// <summary>Hosts the dashboard page. Navigation parameter carries the shared ViewModel.</summary>
public sealed partial class MainWindow : Window
{
    public MainWindow(DashboardViewModel vm)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootFrame.Navigate(typeof(ShellPage), vm);
    }
}
