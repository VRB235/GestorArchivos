using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.Application.Models.MediaVault;
using MediaVault.LinkHub.Infrastructure.Media;

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
        else if (MediaFileExtensions.IsVideo(entry.FullPath))
        {
            var folderPath = Path.GetDirectoryName(entry.FullPath);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                var actressPicture = FolderSessionPicturePicker.TryLoadThumbnailForItem(
                    folderPath,
                    entry.FullPath);
                if (actressPicture is not null)
                    return actressPicture;
            }
        }

        return WindowsShellThumbnailProvider.GetThumbnail(entry.FullPath, entry.IsDirectory);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
