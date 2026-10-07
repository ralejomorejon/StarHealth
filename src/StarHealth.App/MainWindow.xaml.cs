using H.NotifyIcon.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using StarHealth.App.ViewModels;
using StarHealth.App.Views;
using StarHealth.Core.Localization;
using System.IO;

namespace StarHealth.App;

/// <summary>
/// Hosts the dashboard frame. Owns the tray icon (H.NotifyIcon core):
/// hide-on-close keeps the monitor running, Quit exits for real.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly DashboardViewModel _vm;
    private TrayIconWithContextMenu? _tray;
    private System.Drawing.Icon? _trayIcon;
    private bool _allowClose;

    public MainWindow(DashboardViewModel vm)
    {
        _vm = vm;
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { /* icon is cosmetic */ }

        _vm.TrayUpdate = tip =>
        {
            try { if (_tray?.IsCreated == true) _tray.UpdateToolTip(tip); } catch { }
        };
        _vm.TraySettingsChanged += ApplyTrayVisibility;
        ApplyTrayVisibility();
        AppWindow.Closing += OnClosing;

        RootFrame.Navigate(typeof(ShellPage), vm);
    }

    private void ApplyTrayVisibility()
    {
        try
        {
            if (!_vm.TrayEnabled)
            {
                _tray?.Hide();
                return;
            }
            EnsureTray();
            _tray?.Show();
        }
        catch { /* tray is best-effort (remote sessions, Server Core) */ }
    }

    private void EnsureTray()
    {
        if (_tray is not null) return;
        var tray = new TrayIconWithContextMenu("StarHealth");
        try
        {
            // Prefer the shipped .ico (the exe itself only carries the default
            // dotnet icon). Dev layout nests it under AppX/, installed layout
            // keeps it next to the exe; fall back to the associated icon.
            var baseDir = AppContext.BaseDirectory;
            var icoPath = File.Exists(Path.Combine(baseDir, "Assets", "AppIcon.ico"))
                ? Path.Combine(baseDir, "Assets", "AppIcon.ico")
                : Path.Combine(baseDir, "AppX", "Assets", "AppIcon.ico");
            _trayIcon = File.Exists(icoPath)
                ? new System.Drawing.Icon(icoPath)
                : System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            if (_trayIcon is not null) tray.Icon = _trayIcon.Handle;
        }
        catch { }
        tray.ToolTip = "StarHealth";
        var menu = new PopupMenu();
        menu.Items.Add(new PopupMenuItem(Text.Get("tray.show"), (_, _) =>
            DispatcherQueue.TryEnqueue(ShowWindow)));
        menu.Items.Add(new PopupMenuSeparator());
        menu.Items.Add(new PopupMenuItem(Text.Get("tray.quit"), (_, _) =>
            DispatcherQueue.TryEnqueue(Quit)));
        tray.ContextMenu = menu;
        tray.MessageWindow.SubscribeToMouseEventReceived((_, e) =>
        {
            if (e.MouseEvent == MouseEvent.IconLeftDoubleClick)
                DispatcherQueue.TryEnqueue(ShowWindow);
        });
        tray.Create();
        try
        {
            // Belt and suspenders: some shells only pick the icon up on update.
            if (_trayIcon is not null) tray.UpdateIcon(_trayIcon.Handle);
        }
        catch { }
        _tray = tray;
    }

    private void ShowWindow()
    {
        AppWindow.Show();
        Activate();
    }

    private void Quit()
    {
        _allowClose = true;
        try { _tray?.Dispose(); } catch { }
        _tray = null;
        try { _trayIcon?.Dispose(); } catch { }
        _trayIcon = null;
        Close();
    }

    private void OnClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (_vm.TrayEnabled && !_allowClose)
        {
            e.Cancel = true;
            AppWindow.Hide();
        }
    }
}
