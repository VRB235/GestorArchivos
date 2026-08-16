using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.MediaVault;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Media;
using MediaVault.LinkHub.Infrastructure.Launchers;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class MediaVaultService : IMediaVaultService
{
  private readonly IDbContextFactory<AppDbContext> _contextFactory;

  public MediaVaultService(IDbContextFactory<AppDbContext> contextFactory)
  {
    _contextFactory = contextFactory;
  }

  public async Task<IndexDirectoryResult> IndexDirectoryAsync(
    string rootPath,
    IProgress<string>? progress = null,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(rootPath))
      throw new ArgumentException("La ruta raíz es obligatoria.", nameof(rootPath));

    var fullRootPath = Path.GetFullPath(rootPath);
    if (!Directory.Exists(fullRootPath))
      throw new DirectoryNotFoundException($"No existe el directorio: {fullRootPath}");

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var existingByPath = await context.MediaFiles
      .ToDictionaryAsync(file => file.Path, StringComparer.OrdinalIgnoreCase, cancellationToken)
      .ConfigureAwait(false);

    var filesIndexed = 0;
    var filesAdded = 0;
    var filesUpdated = 0;
    var filesSkipped = 0;

    foreach (var filePath in EnumerateMediaFilesSafe(fullRootPath))
    {
      cancellationToken.ThrowIfCancellationRequested();
      progress?.Report(filePath);

      if (!MediaFileExtensions.IsSupported(filePath))
      {
        filesSkipped++;
        continue;
      }

      filesIndexed++;
      var normalizedPath = Path.GetFullPath(filePath);
      var fileName = Path.GetFileName(normalizedPath);
      var extension = Path.GetExtension(normalizedPath);

      if (existingByPath.TryGetValue(normalizedPath, out var existing))
      {
        var changed = false;

        if (!string.Equals(existing.Name, fileName, StringComparison.Ordinal))
        {
          existing.Name = fileName;
          changed = true;
        }

        if (!string.Equals(existing.Extension, extension, StringComparison.OrdinalIgnoreCase))
        {
          existing.Extension = extension;
          changed = true;
        }

        if (changed)
          filesUpdated++;
        else
          filesSkipped++;

        continue;
      }

      var mediaFile = new MediaFile
      {
        Path = normalizedPath,
        Name = fileName,
        Extension = extension
      };

      context.MediaFiles.Add(mediaFile);
      existingByPath[normalizedPath] = mediaFile;
      filesAdded++;
    }

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    return new IndexDirectoryResult
    {
      RootPath = fullRootPath,
      FilesIndexed = filesIndexed,
      FilesAdded = filesAdded,
      FilesUpdated = filesUpdated,
      FilesSkipped = filesSkipped
    };
  }

  public async Task<IReadOnlyList<MediaVaultBrowserEntry>> ListDirectoryEntriesAsync(
    string directoryPath,
    string indexRootPath,
    bool includeHidden = false,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(directoryPath))
      throw new ArgumentException("La ruta del directorio es obligatoria.", nameof(directoryPath));

    if (string.IsNullOrWhiteSpace(indexRootPath))
      throw new ArgumentException("La ruta raíz de indexación es obligatoria.", nameof(indexRootPath));

    var fullRootPath = Path.GetFullPath(indexRootPath);
    var fullDirectoryPath = Path.GetFullPath(directoryPath);

    EnsurePathIsWithinRoot(fullDirectoryPath, fullRootPath);

    if (!Directory.Exists(fullDirectoryPath))
      throw new DirectoryNotFoundException($"No existe el directorio: {fullDirectoryPath}");

    var entries = new List<MediaVaultBrowserEntry>();

    foreach (var subdirectory in EnumerateDirectoriesSafe(fullDirectoryPath, includeHidden))
    {
      DateTime? createdAtUtc = null;
      DateTime? modifiedAtUtc = null;

      try
      {
        var directoryInfo = new DirectoryInfo(subdirectory);
        createdAtUtc = directoryInfo.CreationTimeUtc;
        modifiedAtUtc = directoryInfo.LastWriteTimeUtc;
      }
      catch
      {
        // Sin metadatos si el directorio no es accesible.
      }

      entries.Add(new MediaVaultBrowserEntry
      {
        Name = Path.GetFileName(subdirectory),
        FullPath = subdirectory,
        IsDirectory = true,
        FileType = "Carpeta",
        CreatedAtUtc = createdAtUtc,
        ModifiedAtUtc = modifiedAtUtc
      });
    }

    var filePaths = EnumerateFilesSafe(fullDirectoryPath, includeHidden)
      .Select(Path.GetFullPath)
      .ToList();

    var indexedByPath = await LoadIndexedFilesByPathAsync(filePaths, cancellationToken).ConfigureAwait(false);

    foreach (var filePath in filePaths)
    {
      indexedByPath.TryGetValue(filePath, out var mediaFile);

      DateTime? createdAtUtc = null;
      DateTime? modifiedAtUtc = null;
      var extension = Path.GetExtension(filePath);

      try
      {
        var fileInfo = new FileInfo(filePath);
        createdAtUtc = fileInfo.CreationTimeUtc;
        modifiedAtUtc = fileInfo.LastWriteTimeUtc;
      }
      catch
      {
        // Sin metadatos si el archivo no es accesible.
      }

      entries.Add(new MediaVaultBrowserEntry
      {
        Name = Path.GetFileName(filePath),
        FullPath = filePath,
        IsDirectory = false,
        FileType = string.IsNullOrWhiteSpace(extension) ? "Archivo" : extension.ToLowerInvariant(),
        CreatedAtUtc = createdAtUtc,
        ModifiedAtUtc = modifiedAtUtc,
        MediaFile = mediaFile
      });
    }

    return entries;
  }

  public async Task<IReadOnlyList<MediaFile>> GetAllAsync(CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    return await context.MediaFiles
      .AsNoTracking()
      .OrderBy(file => file.Name)
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);
  }

  public async Task<MediaFile?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    return await context.MediaFiles.AsNoTracking().FirstOrDefaultAsync(file => file.Id == id, cancellationToken).ConfigureAwait(false);
  }

  public async Task<MediaFile?> GetByPathAsync(string path, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(path))
      return null;

    var normalizedPath = Path.GetFullPath(path);
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    return await FindByPathIgnoreCaseAsync(
        context.MediaFiles.AsNoTracking().Include(file => file.Categories).Include(file => file.Actresses).Include(file => file.Producers),
        normalizedPath,
        cancellationToken).ConfigureAwait(false);
  }

  /// <summary>
  /// Garantiza que un archivo multimedia exista en el índice SQLite (crea el registro si falta).
  /// </summary>
  public async Task<MediaFile> EnsureIndexedAsync(string filePath, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(filePath))
      throw new ArgumentException("La ruta del archivo es obligatoria.", nameof(filePath));

    var normalizedPath = Path.GetFullPath(filePath);
    if (!File.Exists(normalizedPath))
      throw new FileNotFoundException("No se encontró el archivo a indexar.", normalizedPath);

    if (!MediaFileExtensions.IsSupported(normalizedPath))
      throw new InvalidOperationException("Solo se pueden indexar archivos de imagen o video soportados.");

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var existing = await FindByPathIgnoreCaseAsync(
        context.MediaFiles.Include(file => file.Categories).Include(file => file.Actresses).Include(file => file.Producers),
        normalizedPath,
        cancellationToken).ConfigureAwait(false);

    if (existing is not null)
      return existing;

    var entity = new MediaFile
    {
      Path = normalizedPath,
      Name = Path.GetFileName(normalizedPath),
      Extension = Path.GetExtension(normalizedPath)
    };

    context.MediaFiles.Add(entity);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task<MediaFile> RenameFileAsync(
    int id,
    string newName,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(newName))
      throw new ArgumentException("El nuevo nombre es obligatorio.", nameof(newName));

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles.FirstOrDefaultAsync(file => file.Id == id, cancellationToken).ConfigureAwait(false)
      ?? throw new KeyNotFoundException($"No se encontró el archivo con Id {id}.");

    if (!File.Exists(entity.Path))
      throw new FileNotFoundException("El archivo ya no existe en disco.", entity.Path);

    var directory = Path.GetDirectoryName(entity.Path)
      ?? throw new InvalidOperationException("No se pudo resolver el directorio del archivo.");

    var sanitizedName = Path.GetFileNameWithoutExtension(newName.Trim());
    if (string.IsNullOrWhiteSpace(sanitizedName))
      throw new ArgumentException("El nuevo nombre no es válido.", nameof(newName));

    var newPath = Path.Combine(directory, sanitizedName + entity.Extension);
    if (File.Exists(newPath) && !string.Equals(newPath, entity.Path, StringComparison.OrdinalIgnoreCase))
      throw new IOException($"Ya existe un archivo con el nombre '{sanitizedName}{entity.Extension}'.");

    File.Move(entity.Path, newPath);

    entity.Path = Path.GetFullPath(newPath);
    entity.Name = Path.GetFileName(newPath);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public Task CreateDirectoryAsync(
    string parentPath,
    string directoryName,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    if (string.IsNullOrWhiteSpace(parentPath))
      throw new ArgumentException("La ruta padre es obligatoria.", nameof(parentPath));

    if (string.IsNullOrWhiteSpace(directoryName))
      throw new ArgumentException("El nombre de la carpeta es obligatorio.", nameof(directoryName));

    var sanitizedName = directoryName.Trim();
    if (sanitizedName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
      throw new ArgumentException("El nombre de la carpeta contiene caracteres inválidos.", nameof(directoryName));

    var fullParentPath = Path.GetFullPath(parentPath);
    if (!Directory.Exists(fullParentPath))
      throw new DirectoryNotFoundException($"No existe el directorio padre: {fullParentPath}");

    Directory.CreateDirectory(Path.Combine(fullParentPath, sanitizedName));
    return Task.CompletedTask;
  }

  public async Task DeleteDirectoryAsync(
    string directoryPath,
    string indexRootPath,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(directoryPath))
      throw new ArgumentException("La ruta de la carpeta es obligatoria.", nameof(directoryPath));

    if (string.IsNullOrWhiteSpace(indexRootPath))
      throw new ArgumentException("La ruta raíz de indexación es obligatoria.", nameof(indexRootPath));

    var fullDirectoryPath = Path.GetFullPath(directoryPath);
    var fullRootPath = Path.GetFullPath(indexRootPath);
    var comparison = OperatingSystem.IsWindows()
      ? StringComparison.OrdinalIgnoreCase
      : StringComparison.Ordinal;

    EnsurePathIsWithinRoot(fullDirectoryPath, fullRootPath);

    if (string.Equals(fullDirectoryPath, fullRootPath, comparison))
      throw new InvalidOperationException("No se puede eliminar la carpeta raíz de indexación.");

    if (!Directory.Exists(fullDirectoryPath))
      throw new DirectoryNotFoundException($"No existe el directorio: {fullDirectoryPath}");

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var prefix = fullDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
      + Path.DirectorySeparatorChar;

    var indexedFiles = await context.MediaFiles.ToListAsync(cancellationToken).ConfigureAwait(false);
    var toRemove = indexedFiles
      .Where(file =>
      {
        try
        {
          var fullFilePath = Path.GetFullPath(file.Path);
          return fullFilePath.StartsWith(prefix, comparison);
        }
        catch
        {
          return false;
        }
      })
      .ToList();

    if (toRemove.Count > 0)
    {
      context.MediaFiles.RemoveRange(toRemove);
      await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    Directory.Delete(fullDirectoryPath, recursive: true);
  }

  public async Task<MediaFile?> MoveFileAsync(
    string sourcePath,
    string destinationDirectoryPath,
    string indexRootPath,
    CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(sourcePath))
      throw new ArgumentException("La ruta del archivo origen es obligatoria.", nameof(sourcePath));

    if (string.IsNullOrWhiteSpace(destinationDirectoryPath))
      throw new ArgumentException("La carpeta destino es obligatoria.", nameof(destinationDirectoryPath));

    if (string.IsNullOrWhiteSpace(indexRootPath))
      throw new ArgumentException("La ruta raíz de indexación es obligatoria.", nameof(indexRootPath));

    var fullRootPath = Path.GetFullPath(indexRootPath);
    var fullSourcePath = Path.GetFullPath(sourcePath);
    var fullDestinationDirectory = Path.GetFullPath(destinationDirectoryPath);

    EnsurePathIsWithinRoot(fullSourcePath, fullRootPath);
    EnsurePathIsWithinRoot(fullDestinationDirectory, fullRootPath);

    if (!File.Exists(fullSourcePath))
      throw new FileNotFoundException("El archivo origen no existe.", fullSourcePath);

    if (!Directory.Exists(fullDestinationDirectory))
      throw new DirectoryNotFoundException($"No existe la carpeta destino: {fullDestinationDirectory}");

    var sourceDirectory = Path.GetDirectoryName(fullSourcePath)
      ?? throw new InvalidOperationException("No se pudo resolver el directorio del archivo origen.");

    if (string.Equals(sourceDirectory, fullDestinationDirectory, StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException("El archivo ya se encuentra en la carpeta seleccionada.");

    var fileName = Path.GetFileName(fullSourcePath);
    var destinationPath = Path.GetFullPath(Path.Combine(fullDestinationDirectory, fileName));

    if (File.Exists(destinationPath))
      throw new IOException($"Ya existe un archivo llamado '{fileName}' en la carpeta destino.");

    File.Move(fullSourcePath, destinationPath);

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    var entity = await FindByPathIgnoreCaseAsync(
        context.MediaFiles.Include(file => file.Categories).Include(file => file.Actresses).Include(file => file.Producers),
        fullSourcePath,
        cancellationToken).ConfigureAwait(false);

    if (entity is null)
      return null;

    entity.Path = destinationPath;
    entity.Name = Path.GetFileName(destinationPath);
    entity.Extension = Path.GetExtension(destinationPath);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task DeleteFileAsync(int id, CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles.FirstOrDefaultAsync(file => file.Id == id, cancellationToken).ConfigureAwait(false);
    if (entity is null)
      return;

    if (File.Exists(entity.Path))
      File.Delete(entity.Path);

    context.MediaFiles.Remove(entity);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }

  public async Task<MediaFile> UpdateRankingsAsync(
    int id,
    double rankingCalidad,
    double rankingContenido,
    double rankingGusto,
    CancellationToken cancellationToken = default)
  {
    ValidateRanking(rankingCalidad, nameof(rankingCalidad));
    ValidateRanking(rankingContenido, nameof(rankingContenido));
    ValidateRanking(rankingGusto, nameof(rankingGusto));

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles.FirstOrDefaultAsync(file => file.Id == id, cancellationToken).ConfigureAwait(false)
      ?? throw new KeyNotFoundException($"No se encontró el archivo con Id {id}.");

    entity.RankingCalidad = rankingCalidad;
    entity.RankingContenido = rankingContenido;
    entity.RankingGusto = rankingGusto;

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    await context.Entry(entity).Collection(file => file.Categories).LoadAsync(cancellationToken).ConfigureAwait(false);
    await context.Entry(entity).Collection(file => file.Actresses).LoadAsync(cancellationToken).ConfigureAwait(false);
    await context.Entry(entity).Collection(file => file.Producers).LoadAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task<MediaMetadataResetResult> ClearAllMediaMetadataAsync(CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

    var categoryLinksRemoved = await context.Database
      .ExecuteSqlRawAsync("DELETE FROM MediaFileCategories", cancellationToken)
      .ConfigureAwait(false);

    var categoriesDeleted = await context.VideoCategories
      .ExecuteDeleteAsync(cancellationToken)
      .ConfigureAwait(false);

    var actressLinksRemoved = await context.Database
      .ExecuteSqlRawAsync("DELETE FROM MediaFileActresses", cancellationToken)
      .ConfigureAwait(false);

    var actressesDeleted = await context.Actresses
      .ExecuteDeleteAsync(cancellationToken)
      .ConfigureAwait(false);

    var producerLinksRemoved = await context.Database
      .ExecuteSqlRawAsync("DELETE FROM MediaFileProducers", cancellationToken)
      .ConfigureAwait(false);

    var producersDeleted = await context.Producers
      .ExecuteDeleteAsync(cancellationToken)
      .ConfigureAwait(false);

    var filesUpdated = await context.MediaFiles
      .Where(file =>
        file.VecesAbierto != 0 ||
        file.RankingCalidad != 0 ||
        file.RankingContenido != 0 ||
        file.RankingGusto != 0)
      .ExecuteUpdateAsync(
        setters => setters
          .SetProperty(file => file.VecesAbierto, 0)
          .SetProperty(file => file.RankingCalidad, 0.0)
          .SetProperty(file => file.RankingContenido, 0.0)
          .SetProperty(file => file.RankingGusto, 0.0),
        cancellationToken)
      .ConfigureAwait(false);

    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

    return new MediaMetadataResetResult
    {
      FilesUpdated = filesUpdated,
      CategoryLinksRemoved = categoryLinksRemoved,
      CategoriesDeleted = categoriesDeleted,
      ActressLinksRemoved = actressLinksRemoved,
      ActressesDeleted = actressesDeleted,
      ProducerLinksRemoved = producerLinksRemoved,
      ProducersDeleted = producersDeleted
    };
  }

  public async Task<MediaFile> UpdateCategoriesAsync(
    int id,
    IReadOnlyCollection<int> categoryIds,
    CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles
      .Include(file => file.Categories).Include(file => file.Actresses).Include(file => file.Producers)
      .FirstOrDefaultAsync(file => file.Id == id, cancellationToken)
      .ConfigureAwait(false)
      ?? throw new KeyNotFoundException($"No se encontró el archivo con Id {id}.");

    if (!MediaFileExtensions.IsSupported(entity.Path))
      throw new InvalidOperationException("Solo los archivos multimedia indexados pueden tener categoría.");

    var distinctIds = categoryIds.Distinct().ToList();

    if (distinctIds.Count > 0)
    {
      var existingIds = await context.VideoCategories
        .Where(category => distinctIds.Contains(category.Id))
        .Select(category => category.Id)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

      if (existingIds.Count != distinctIds.Count)
        throw new KeyNotFoundException("Una o más categorías seleccionadas no existen.");
    }

    entity.Categories.Clear();

    if (distinctIds.Count > 0)
    {
      var categories = await context.VideoCategories
        .Where(category => distinctIds.Contains(category.Id))
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

      foreach (var category in categories)
        entity.Categories.Add(category);
    }

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task<MediaFile> UpdateActressesAsync(
    int id,
    IReadOnlyCollection<int> actressIds,
    CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles
      .Include(file => file.Categories)
      .Include(file => file.Actresses).Include(file => file.Producers)
      .FirstOrDefaultAsync(file => file.Id == id, cancellationToken)
      .ConfigureAwait(false)
      ?? throw new KeyNotFoundException($"No se encontró el archivo con Id {id}.");

    if (!MediaFileExtensions.IsVideo(entity.Path))
      throw new InvalidOperationException("Solo los videos indexados pueden tener actrices asignadas.");

    var distinctIds = actressIds.Distinct().ToList();

    if (distinctIds.Count > 0)
    {
      var existingIds = await context.Actresses
        .Where(actress => distinctIds.Contains(actress.Id))
        .Select(actress => actress.Id)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

      if (existingIds.Count != distinctIds.Count)
        throw new KeyNotFoundException("Una o más actrices seleccionadas no existen.");
    }

    entity.Actresses.Clear();

    if (distinctIds.Count > 0)
    {
      var actresses = await context.Actresses
        .Where(actress => distinctIds.Contains(actress.Id))
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

      foreach (var actress in actresses)
        entity.Actresses.Add(actress);
    }

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task<MediaFile> UpdateProducersAsync(
    int id,
    IReadOnlyCollection<int> producerIds,
    CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles
      .Include(file => file.Categories)
      .Include(file => file.Actresses)
      .Include(file => file.Producers)
      .FirstOrDefaultAsync(file => file.Id == id, cancellationToken)
      .ConfigureAwait(false)
      ?? throw new KeyNotFoundException($"No se encontró el archivo con Id {id}.");

    if (!MediaFileExtensions.IsVideo(entity.Path))
      throw new InvalidOperationException("Solo los videos indexados pueden tener productoras asignadas.");

    var distinctIds = producerIds.Distinct().ToList();

    if (distinctIds.Count > 0)
    {
      var existingIds = await context.Producers
        .Where(producer => distinctIds.Contains(producer.Id))
        .Select(producer => producer.Id)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

      if (existingIds.Count != distinctIds.Count)
        throw new KeyNotFoundException("Una o más productoras seleccionadas no existen.");
    }

    entity.Producers.Clear();

    if (distinctIds.Count > 0)
    {
      var producers = await context.Producers
        .Where(producer => distinctIds.Contains(producer.Id))
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

      foreach (var producer in producers)
        entity.Producers.Add(producer);
    }

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public Task<IReadOnlyList<MediaFile>> FindVideosByActressIdsAsync(
    IReadOnlyCollection<int> actressIds,
    CancellationToken cancellationToken = default) =>
    FindVideosByFiltersAsync(actressIds, categoryIds: [], producerIds: [], cancellationToken);

  public async Task<IReadOnlyList<MediaFile>> FindVideosByFiltersAsync(
    IReadOnlyCollection<int> actressIds,
    IReadOnlyCollection<int> categoryIds,
    IReadOnlyCollection<int> producerIds,
    CancellationToken cancellationToken = default)
  {
    var distinctActressIds = actressIds.Distinct().ToList();
    var distinctCategoryIds = categoryIds.Distinct().ToList();
    var distinctProducerIds = producerIds.Distinct().ToList();

    if (distinctActressIds.Count == 0 && distinctCategoryIds.Count == 0 && distinctProducerIds.Count == 0)
      return [];

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var query = context.MediaFiles
      .AsNoTracking()
      .Include(file => file.Categories)
      .Include(file => file.Actresses)
      .Include(file => file.Producers)
      .AsQueryable();

    if (distinctActressIds.Count > 0)
      query = query.Where(file => file.Actresses.Any(actress => distinctActressIds.Contains(actress.Id)));

    if (distinctCategoryIds.Count > 0)
      query = query.Where(file => file.Categories.Any(category => distinctCategoryIds.Contains(category.Id)));

    if (distinctProducerIds.Count > 0)
      query = query.Where(file => file.Producers.Any(producer => distinctProducerIds.Contains(producer.Id)));

    var files = await query
      .OrderBy(file => file.Name)
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    return files
      .Where(file => MediaFileExtensions.IsVideo(file.Path))
      .ToList();
  }

  public async Task<MediaFile?> OpenFileAsync(
    int id,
    bool preferVlc = false,
    CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.MediaFiles
      .Include(file => file.Categories).Include(file => file.Actresses).Include(file => file.Producers)
      .FirstOrDefaultAsync(file => file.Id == id, cancellationToken)
      .ConfigureAwait(false);
    if (entity is null || !File.Exists(entity.Path))
      return null;

    if (!MediaFileLauncher.TryOpen(entity.Path, preferVlc))
      return null;

    entity.VecesAbierto++;
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  private static void ValidateRanking(double value, string paramName)
  {
    if (value is < 0 or > MediaFileRankingScale.MaxStars)
      throw new ArgumentOutOfRangeException(paramName, "El ranking debe estar entre 0 y 5 estrellas.");
  }

  private async Task<Dictionary<string, MediaFile>> LoadIndexedFilesByPathAsync(
    IReadOnlyList<string> filePaths,
    CancellationToken cancellationToken)
  {
    var indexedByPath = new Dictionary<string, MediaFile>(StringComparer.OrdinalIgnoreCase);
    if (filePaths.Count == 0)
      return indexedByPath;

    var filePathSet = filePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    // SQLite compara strings con sensibilidad a mayúsculas; se carga y empareja en memoria.
    var indexedFiles = await context.MediaFiles
      .AsNoTracking()
      .Include(file => file.Categories).Include(file => file.Actresses).Include(file => file.Producers)
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    foreach (var file in indexedFiles)
    {
      string normalized;
      try
      {
        normalized = Path.GetFullPath(file.Path);
      }
      catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
      {
        continue;
      }

      if (!filePathSet.Contains(normalized))
        continue;

      indexedByPath.TryAdd(normalized, file);
    }

    return indexedByPath;
  }

  private static async Task<MediaFile?> FindByPathIgnoreCaseAsync(
    IQueryable<MediaFile> query,
    string normalizedPath,
    CancellationToken cancellationToken)
  {
    var exact = await query
      .FirstOrDefaultAsync(file => file.Path == normalizedPath, cancellationToken)
      .ConfigureAwait(false);
    if (exact is not null)
      return exact;

    var fileName = Path.GetFileName(normalizedPath);
    var candidates = await query
      .Where(file => file.Name == fileName)
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    return candidates.FirstOrDefault(file =>
      string.Equals(Path.GetFullPath(file.Path), normalizedPath, StringComparison.OrdinalIgnoreCase));
  }

  private static void EnsurePathIsWithinRoot(string fullPath, string fullRootPath)
  {
    var comparison = OperatingSystem.IsWindows()
      ? StringComparison.OrdinalIgnoreCase
      : StringComparison.Ordinal;

    if (string.Equals(fullPath, fullRootPath, comparison))
      return;

    var rootPrefix = fullRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
      + Path.DirectorySeparatorChar;

    if (!fullPath.StartsWith(rootPrefix, comparison))
      throw new ArgumentException("La ruta está fuera del directorio configurado para indexación.");
  }

  private static EnumerationOptions CreateEnumerationOptions(bool includeHidden) =>
    new()
    {
      AttributesToSkip = includeHidden
        ? FileAttributes.System
        : FileAttributes.Hidden | FileAttributes.System,
      IgnoreInaccessible = true
    };

  private static IEnumerable<string> EnumerateDirectoriesSafe(string directoryPath, bool includeHidden)
  {
    IEnumerable<string> directories;
    try
    {
      directories = Directory.EnumerateDirectories(
        directoryPath,
        "*",
        CreateEnumerationOptions(includeHidden));
    }
    catch (UnauthorizedAccessException)
    {
      yield break;
    }
    catch (DirectoryNotFoundException)
    {
      yield break;
    }

    foreach (var directory in directories)
      yield return Path.GetFullPath(directory);
  }

  private static IEnumerable<string> EnumerateFilesSafe(string directoryPath, bool includeHidden)
  {
    IEnumerable<string> files;
    try
    {
      files = Directory.EnumerateFiles(
        directoryPath,
        "*",
        CreateEnumerationOptions(includeHidden));
    }
    catch (UnauthorizedAccessException)
    {
      yield break;
    }
    catch (DirectoryNotFoundException)
    {
      yield break;
    }

    foreach (var file in files)
      yield return file;
  }

  private static IEnumerable<string> EnumerateMediaFilesSafe(string rootPath)
  {
    var pending = new Stack<string>();
    pending.Push(rootPath);

    while (pending.Count > 0)
    {
      var current = pending.Pop();

      IEnumerable<string> subdirectories;
      try
      {
        subdirectories = Directory.EnumerateDirectories(current);
      }
      catch (UnauthorizedAccessException)
      {
        continue;
      }
      catch (DirectoryNotFoundException)
      {
        continue;
      }

      foreach (var directory in subdirectories)
        pending.Push(directory);

      IEnumerable<string> files;
      try
      {
        files = Directory.EnumerateFiles(current);
      }
      catch (UnauthorizedAccessException)
      {
        continue;
      }
      catch (DirectoryNotFoundException)
      {
        continue;
      }

      foreach (var file in files)
        yield return file;
    }
  }
}
