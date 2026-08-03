using MediaVault.LinkHub.Infrastructure.Data;

namespace MediaVault.LinkHub.Infrastructure.Media;

/// <summary>
/// Copia logos de enlaces a una carpeta gestionada bajo el directorio de datos de la app,
/// de modo que sobrevivan a la eliminación del archivo origen.
/// </summary>
public sealed class LinkLogoStorage
{
    public const string LogosFolderName = "WebLinkLogos";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".ico", ".bmp"
    };

    private readonly string _rootDirectory;

    public LinkLogoStorage(string? rootDirectory = null)
    {
        _rootDirectory = string.IsNullOrWhiteSpace(rootDirectory)
            ? SqliteDatabasePathProvider.GetAppDataDirectory()
            : Path.GetFullPath(rootDirectory);
    }

    public string GetLogosDirectory()
    {
        var directory = Path.Combine(_rootDirectory, LogosFolderName);
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
            var logosRoot = Path.GetFullPath(GetLogosDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return fullPath.StartsWith(logosRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Persiste una copia del logo en el almacén managed. Si ya es managed, retorna la misma ruta.
    /// </summary>
    public string Persist(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("La ruta del logo es obligatoria.", nameof(sourcePath));

        var fullSource = Path.GetFullPath(sourcePath.Trim());
        if (!File.Exists(fullSource))
            throw new FileNotFoundException("No se encontró la imagen del logo.", fullSource);

        if (IsManagedPath(fullSource))
            return fullSource;

        var extension = Path.GetExtension(fullSource);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            throw new ArgumentException(
                $"Extensión de logo no soportada: '{extension}'. Use png, jpg, jpeg, webp, ico o bmp.",
                nameof(sourcePath));

        var destination = Path.Combine(GetLogosDirectory(), $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}");
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
            // Mejor esfuerzo: no fallar el flujo de negocio por cleanup de disco.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
