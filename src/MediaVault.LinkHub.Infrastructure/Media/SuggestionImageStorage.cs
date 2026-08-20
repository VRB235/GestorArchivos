using MediaVault.LinkHub.Infrastructure.Data;

namespace MediaVault.LinkHub.Infrastructure.Media;

/// <summary>
/// Copia capturas/adjuntos de sugerencias a una carpeta managed bajo AppData.
/// </summary>
public sealed class SuggestionImageStorage
{
    public const string AttachmentsFolderName = "SuggestionAttachments";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"
    };

    private readonly string _rootDirectory;

    public SuggestionImageStorage(string? rootDirectory = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? SqliteDatabasePathProvider.GetAppDataDirectory()
            : Path.GetFullPath(rootDirectory);
    }

    public string GetAttachmentsDirectory()
    {
        var directory = Path.Combine(_rootDirectory, AttachmentsFolderName);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetFullPath(GetAttachmentsDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public string Persist(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("La ruta de la imagen es obligatoria.", nameof(sourcePath));

        var fullSource = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(fullSource))
            throw new FileNotFoundException("No se encontró la imagen adjunta.", fullSource);

        if (IsManagedPath(fullSource))
            return fullSource;

        var extension = Path.GetExtension(fullSource);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new ArgumentException(
                $"Extensión no soportada: '{extension}'. Use png, jpg, jpeg, webp, bmp o gif.",
                nameof(sourcePath));

        var destination = Path.Combine(
            GetAttachmentsDirectory(),
            $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
        File.Copy(fullSource, destination, overwrite: false);
        return destination;
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
}
