using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaVault.LinkHub.App.Shell;

internal static class LocalImageLoader
{
    public static ImageSource? TryLoad(string? path, int decodePixelWidth = 128)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
}
