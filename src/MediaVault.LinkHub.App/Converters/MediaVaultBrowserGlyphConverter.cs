using System.Globalization;
using System.IO;
using System.Windows.Data;

using MediaVault.LinkHub.Application.Models.MediaVault;

namespace MediaVault.LinkHub.App.Converters;

public sealed class MediaVaultBrowserGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MediaVaultBrowserEntry entry)
            return "📄";

        if (entry.IsDirectory)
            return "📁";

        var extension = Path.GetExtension(entry.FullPath);
        if (IsVideoExtension(extension))
            return "🎬";

        if (IsImageExtension(extension))
            return "🖼";

        return "📄";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool IsVideoExtension(string extension) =>
        extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".flv", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".3gp", StringComparison.OrdinalIgnoreCase);

    private static bool IsImageExtension(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".heic", StringComparison.OrdinalIgnoreCase);
}
