using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StarHealth.App.ViewModels;

namespace StarHealth.App.Controls;

/// <summary>Dish outages (amber) vs plain dish events (dim).</summary>
public sealed class TimelineTemplateSelector : DataTemplateSelector
{
    public DataTemplate? OutageTemplate { get; set; }
    public DataTemplate? EventTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
        => item is TimelineRow row && row.IsOutage ? OutageTemplate : EventTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
