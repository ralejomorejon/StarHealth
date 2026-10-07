using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StarHealth.App.ViewModels;

namespace StarHealth.App.Controls;

/// <summary>Benign alerts (heater, power-save, routine reboot) render green,
// otherwise amber. Pure WinUI/Uno APIs.</summary>
public sealed class AlertTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GoodTemplate { get; set; }
    public DataTemplate? WarnTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is AlertRow row && row.IsGood ? GoodTemplate : WarnTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
