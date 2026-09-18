using MediaVault.LinkHub.Infrastructure.Data;

namespace MediaVault.LinkHub.Infrastructure.Media;

/// <summary>
/// Carpeta managed de videos descargados bajo AppData.
/// </summary>
public sealed class VideoDownloadStorage
{
    public const string DownloadsFolderName = "VideoDownloads";

    private readonly string _rootDirectory;

    public VideoDownloadStorage(string? rootDirectory = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? SqliteDatabasePathProvider.GetAppDataDirectory()
            : Path.GetFullPath(rootDirectory);
    }

    public string GetDownloadsDirectory()
    {
        var directory = Path.Combine(_rootDirectory, DownloadsFolderName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string BuildOutputPath(int id, string displayName)
    {
        var safeName = SanitizeFileName(displayName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "video";

        return Path.Combine(GetDownloadsDirectory(), $"{id}_{safeName}.ts");
    }

    public bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetFullPath(GetDownloadsDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public void TryDeleteManaged(string? path)
    {
        if (!IsManagedPath(path))
            return;

        try
        {
            var fullPath = Path.GetFullPath(path!);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim(' ', '.', '_');
        return sanitized.Length > 80 ? sanitized[..80] : sanitized;
    }
}
