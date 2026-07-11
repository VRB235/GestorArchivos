namespace MediaVault.LinkHub.Application.Models.Settings;

/// <summary>
/// Preferencias persistentes de la aplicación de escritorio.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Carpeta raíz usada por Media Vault para indexar archivos multimedia.
    /// </summary>
    public string MediaIndexRootPath { get; init; } = string.Empty;

    /// <summary>
    /// Iconos personalizados por ruta completa de carpeta.
    /// </summary>
    public Dictionary<string, string> FolderIconPaths { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Si es true, el explorador de Media Vault incluye archivos y carpetas ocultos.
    /// </summary>
    public bool ShowHiddenFilesAndFolders { get; init; }
}