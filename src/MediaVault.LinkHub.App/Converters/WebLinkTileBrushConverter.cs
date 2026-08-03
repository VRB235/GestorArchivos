using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.Converters;

public sealed class WebLinkTileBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var categoria = value is WebLink link ? link.Categoria : LinkCategory.Oficial;

        var (start, end) = categoria switch
        {
            LinkCategory.Oficial => ("#2563EB", "#1D4ED8"),
            LinkCategory.Descarga => ("#7C3AED", "#5B21B6"),
            LinkCategory.Gratis => ("#059669", "#047857"),
            _ => ("#374151", "#1F2937")
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(start)!, 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(end)!, 1));
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
