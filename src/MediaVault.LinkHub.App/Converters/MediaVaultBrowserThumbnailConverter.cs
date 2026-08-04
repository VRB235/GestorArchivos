using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.Application.Models.MediaVault;

namespace MediaVault.LinkHub.App.Converters;

public sealed class MediaVaultBrowserThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MediaVaultBrowserEntry entry)
            return null;

        if (entry.IsDirectory)
        {
            var sessionPicture = FolderSessionPicturePicker.TryLoadSessionThumbnail(entry.FullPath);
            if (sessionPicture is not null)
                return sessionPicture;

            if (!string.IsNullOrWhiteSpace(entry.CustomIconPath))
                return LocalImageLoader.TryLoad(entry.CustomIconPath);
        }

        return WindowsShellThumbnailProvider.GetThumbnail(entry.FullPath, entry.IsDirectory);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
