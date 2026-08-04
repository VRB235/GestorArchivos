using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Elige una imagen aleatoria de <c>{carpeta}/Pictures</c> y la reutiliza durante la vida del proceso.
/// </summary>
internal static class FolderSessionPicturePicker
{
    private static readonly ConcurrentDictionary<string, string?> SessionPicks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".webp",
        ".bmp",
        ".gif"
    };

    /// <summary>
    /// Ruta de imagen elegida para la carpeta en esta sesión, o null si no hay Pictures.
    /// </summary>
    public static string? GetSessionPicturePath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        string normalized;
        try
        {
            normalized = Path.GetFullPath(folderPath);
        }
        catch (Exception)
        {
            return null;
        }

        return SessionPicks.GetOrAdd(normalized, PickRandomPicture);
    }

    public static ImageSource? TryLoadSessionThumbnail(string folderPath, int decodePixelWidth = 128)
    {
        var picturePath = GetSessionPicturePath(folderPath);
        return LocalImageLoader.TryLoad(picturePath, decodePixelWidth);
    }

    private static string? PickRandomPicture(string folderPath)
    {
        var picturesDirectory = Path.Combine(folderPath, "Pictures");
        if (!Directory.Exists(picturesDirectory))
            return null;

        string[] files;
        try
        {
            files = Directory.EnumerateFiles(picturesDirectory)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (files.Length == 0)
            return null;

        return files[Random.Shared.Next(files.Length)];
    }
}
