using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace MediaVault.LinkHub.App.Converters;

public sealed class BooleanToWaitCursorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Cursors.Wait : Cursors.Arrow;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
