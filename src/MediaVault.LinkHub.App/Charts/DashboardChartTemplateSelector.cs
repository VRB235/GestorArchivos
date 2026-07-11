using System.Windows;
using System.Windows.Controls;
using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Charts;

public sealed class DashboardChartTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BarChartTemplate { get; set; }

    public DataTemplate? PieChartTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item is DashboardChartPanelViewModel { IsPie: true }
            ? PieChartTemplate
            : BarChartTemplate;
}
