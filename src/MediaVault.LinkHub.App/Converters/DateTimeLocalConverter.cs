using System.Globalization;
using System.Windows.Data;

namespace MediaVault.LinkHub.App.Converters;

public sealed class DateTimeLocalConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return "Sin registrar";

        var local = dateTime.Kind == DateTimeKind.Utc
            ? dateTime.ToLocalTime()
            : dateTime;

        return local.ToString("dd/MM/yyyy HH:mm", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
