using System.Globalization;
using System.Windows.Data;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.Converters;

public sealed class LinkCategoryGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LinkCategory categoria
            ? categoria switch
            {
                LinkCategory.Oficial => "🌐",
                LinkCategory.Descarga => "⬇",
                LinkCategory.Gratis => "★",
                _ => "🔗"
            }
            : "🔗";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
