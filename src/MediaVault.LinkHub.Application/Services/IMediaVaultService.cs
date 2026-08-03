using MediaVault.LinkHub.Application.Models.MediaVault;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Contrato del módulo File &amp; Media Vault: indexación, exploración y operaciones sobre archivos.
/// </summary>
public interface IMediaVaultService
{
    /// <summary>
    /// Analiza de forma recursiva una ruta local e indexa imágenes y videos en SQLite.
    /// </summary>
    Task<IndexDirectoryResult> IndexDirectoryAsync(
        string rootPath,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista carpetas y archivos multimedia del directorio indicado (un nivel).
    /// </summary>
    Task<IReadOnlyList<MediaVaultBrowserEntry>> ListDirectoryEntriesAsync(
        string directoryPath,
        string indexRootPath,
        bool includeHidden = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<MediaFile?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<MediaFile?> GetByPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Garantiza que un archivo multimedia exista en el índice (lo crea si aún no está).
    /// </summary>
    Task<MediaFile> EnsureIndexedAsync(string filePath, CancellationToken cancellationToken = default);

    Task<MediaFile> RenameFileAsync(
        int id,
        string newName,
        CancellationToken cancellationToken = default);

    Task CreateDirectoryAsync(
        string parentPath,
        string directoryName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mueve un archivo a otra carpeta dentro del root de indexación y actualiza el índice si existe.
    /// </summary>
    /// <returns>La entidad indexada actualizada, o <c>null</c> si el archivo no estaba en el índice.</returns>
    Task<MediaFile?> MoveFileAsync(
        string sourcePath,
        string destinationDirectoryPath,
        string indexRootPath,
        CancellationToken cancellationToken = default);

    Task DeleteFileAsync(int id, CancellationToken cancellationToken = default);

    Task<MediaFile> UpdateRankingsAsync(
        int id,
        double rankingCalidad,
        double rankingContenido,
        double rankingGusto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Restablece rankings, contador de aperturas y categorías de todos los archivos indexados.
    /// </summary>
    Task<MediaMetadataResetResult> ClearAllMediaMetadataAsync(CancellationToken cancellationToken = default);

    Task<MediaFile> UpdateCategoriesAsync(
        int id,
        IReadOnlyCollection<int> categoryIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta el archivo e incrementa <see cref="MediaFile.VecesAbierto"/>.
    /// </summary>
    /// <returns>La entidad actualizada, o <c>null</c> si no se pudo abrir.</returns>
    /// <param name="preferVlc">Si es true, intenta abrir con VLC cuando esté instalado.</param>
    Task<MediaFile?> OpenFileAsync(
        int id,
        bool preferVlc = false,
        CancellationToken cancellationToken = default);
}
