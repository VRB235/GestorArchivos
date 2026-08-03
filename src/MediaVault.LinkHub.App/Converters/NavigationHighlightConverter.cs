using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MediaVault.LinkHub.App.Converters;

public sealed class NavigationHighlightConverter : IMultiValueConverter
{
    public Brush ActiveBrush { get; set; } = new SolidColorBrush(Color.FromRgb(59, 130, 246));

    public Brush InactiveBrush { get; set; } = Brushes.Transparent;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return InactiveBrush;

        var selected = values[0]?.ToString();
        var target = values[1]?.ToString();
        return string.Equals(selected, target, StringComparison.Ordinal) ? ActiveBrush : InactiveBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
