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
    /// Elimina una carpeta (recursivo) dentro del root de indexación y limpia el índice de archivos bajo ella.
    /// No permite borrar la carpeta raíz de indexación.
    /// </summary>
    Task DeleteDirectoryAsync(
        string directoryPath,
        string indexRootPath,
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

    /// <summary>
    /// Elimina del índice (no del disco) rutas de papelera/sistema, fuera del root y opcionalmente inexistentes.
    /// </summary>
    Task<IndexPurgeResult> PurgeInvalidIndexEntriesAsync(
        string? indexRootPath = null,
        bool removeMissingFiles = true,
        CancellationToken cancellationToken = default);

    Task<MediaFile> UpdateCategoriesAsync(
        int id,
        IReadOnlyCollection<int> categoryIds,
        CancellationToken cancellationToken = default);

    Task<MediaFile> UpdateActressesAsync(
        int id,
        IReadOnlyCollection<int> actressIds,
        CancellationToken cancellationToken = default);

    Task<MediaFile> UpdateProducersAsync(
        int id,
        IReadOnlyCollection<int> producerIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Videos indexados filtrados por actrices, categorías y/o productoras.
    /// Dentro de cada dimensión el criterio es OR; entre dimensiones es AND.
    /// </summary>
    Task<IReadOnlyList<MediaFile>> FindVideosByFiltersAsync(
        IReadOnlyCollection<int> actressIds,
        IReadOnlyCollection<int> categoryIds,
        IReadOnlyCollection<int> producerIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Videos indexados que tienen al menos una de las actrices indicadas (filtro OR).
    /// </summary>
    Task<IReadOnlyList<MediaFile>> FindVideosByActressIdsAsync(
        IReadOnlyCollection<int> actressIds,
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

    /// <summary>
    /// Rutas de miniaturas asignadas al video (ordenadas por <c>SortOrder</c>).
    /// </summary>
    Task<IReadOnlyList<string>> GetThumbnailPathsAsync(
        int mediaFileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Miniaturas asignadas para un lote de rutas de video (clave = ruta normalizada).
    /// Solo incluye videos con al menos una imagen asignada.
    /// </summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetThumbnailPathsByVideoPathsAsync(
        IReadOnlyCollection<string> videoPaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplaza las miniaturas asignadas al video. Rutas inexistentes o no imagen se ignoran.
    /// </summary>
    Task<IReadOnlyList<string>> SetThumbnailPathsAsync(
        int mediaFileId,
        IReadOnlyCollection<string> imagePaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imágenes disponibles en <c>{carpeta_del_video}/Pictures</c>.
    /// </summary>
    Task<IReadOnlyList<string>> ListPicturesForVideoAsync(
        string videoPath,
        CancellationToken cancellationToken = default);
}
