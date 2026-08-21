using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Asigna fotos de <c>{carpeta}/Pictures</c> a ítems (videos) evitando repetir
/// mientras haya fotos distintas disponibles. Si un video tiene miniaturas
/// asignadas en BD, elige solo entre esas N rutas. Las asignaciones viven con el proceso.
/// </summary>
internal static class FolderSessionPicturePicker
{
    private static readonly ConcurrentDictionary<string, FolderPicturePool> Pools =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> DedicatedPictures =
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
    /// Registra (o limpia) el conjunto de miniaturas exclusivas de un video.
    /// Esas rutas se excluyen del pool compartido de la carpeta.
    /// </summary>
    public static void RegisterDedicatedPictures(string itemKey, IReadOnlyList<string>? imagePaths)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return;

        string normalizedKey;
        try
        {
            normalizedKey = Path.GetFullPath(itemKey);
        }
        catch (Exception)
        {
            return;
        }

        if (imagePaths is null || imagePaths.Count == 0)
        {
            DedicatedPictures.TryRemove(normalizedKey, out _);
            InvalidateDedicatedPool(normalizedKey);
            return;
        }

        var normalizedPaths = imagePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path =>
            {
                try
                {
                    return Path.GetFullPath(path);
                }
                catch (Exception)
                {
                    return null;
                }
            })
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            DedicatedPictures.TryRemove(normalizedKey, out _);
            InvalidateDedicatedPool(normalizedKey);
            return;
        }

        DedicatedPictures[normalizedKey] = normalizedPaths;
        InvalidateDedicatedPool(normalizedKey);
    }

    /// <summary>
    /// Prefija asignaciones distintas para un lote (p. ej. 5 recomendaciones).
    /// </summary>
    public static void PrefetchDistinctAssignments(
        IEnumerable<(string ItemKey, string FolderPath)> items)
    {
        foreach (var group in items.GroupBy(
                     item => NormalizeFolder(item.FolderPath),
                     StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
                continue;

            var pool = Pools.GetOrAdd(group.Key, static path => new FolderPicturePool(path));
            var keys = group
                .Select(item => item.ItemKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            pool.AssignDistinct(keys);
        }
    }

    /// <summary>
    /// Foto estable de carpeta (un ítem sintético) — útil para tiles de directorio.
    /// </summary>
    public static string? GetSessionPicturePath(string folderPath)
    {
        var normalized = NormalizeFolder(folderPath);
        if (normalized is null)
            return null;

        var pool = Pools.GetOrAdd(normalized, static path => new FolderPicturePool(path));
        return pool.Assign(normalized);
    }

    public static ImageSource? TryLoadSessionThumbnail(string folderPath, int decodePixelWidth = 128)
    {
        var picturePath = GetSessionPicturePath(folderPath);
        return LocalImageLoader.TryLoad(picturePath, decodePixelWidth);
    }

    /// <summary>
    /// Foto asignada a un ítem concreto (ruta de video) dentro de la carpeta padre.
    /// </summary>
    public static string? GetPicturePathForItem(string folderPath, string itemKey)
    {
        if (TryGetDedicatedPicture(itemKey, out var dedicated))
            return dedicated;

        var normalized = NormalizeFolder(folderPath);
        if (normalized is null || string.IsNullOrWhiteSpace(itemKey))
            return null;

        var pool = Pools.GetOrAdd(normalized, static path => new FolderPicturePool(path));
        return pool.Assign(itemKey);
    }

    public static ImageSource? TryLoadThumbnailForItem(
        string folderPath,
        string itemKey,
        int decodePixelWidth = 128)
    {
        var picturePath = GetPicturePathForItem(folderPath, itemKey);
        return LocalImageLoader.TryLoad(picturePath, decodePixelWidth);
    }

    public static int CountPictures(string folderPath)
    {
        var normalized = NormalizeFolder(folderPath);
        if (normalized is null)
            return 0;

        var pool = Pools.GetOrAdd(normalized, static path => new FolderPicturePool(path));
        return pool.PictureCount;
    }

    private static bool TryGetDedicatedPicture(string itemKey, out string? picturePath)
    {
        picturePath = null;
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        string normalizedKey;
        try
        {
            normalizedKey = Path.GetFullPath(itemKey);
        }
        catch (Exception)
        {
            return false;
        }

        if (!DedicatedPictures.TryGetValue(normalizedKey, out var pictures) || pictures.Count == 0)
            return false;

        var pool = Pools.GetOrAdd(
            DedicatedPoolKey(normalizedKey),
            _ => new FolderPicturePool(normalizedKey, pictures));

        picturePath = pool.Assign(normalizedKey);
        return picturePath is not null;
    }

    private static void InvalidateDedicatedPool(string normalizedItemKey)
    {
        var dedicatedPoolKey = DedicatedPoolKey(normalizedItemKey);
        Pools.TryRemove(dedicatedPoolKey, out _);

        foreach (var pool in Pools.Values)
            pool.ClearAssignment(normalizedItemKey);

        // Forzar re-evaluación del pool compartido (exclusiones).
        foreach (var pool in Pools.Values)
            pool.InvalidateSharedPictureCache();
    }

    private static string DedicatedPoolKey(string itemKey) =>
        $"dedicated::{itemKey}";

    private static string? NormalizeFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        try
        {
            return Path.GetFullPath(folderPath);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static HashSet<string> GetReservedPicturePaths()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pictures in DedicatedPictures.Values)
        {
            foreach (var path in pictures)
                reserved.Add(path);
        }

        return reserved;
    }

    private sealed class FolderPicturePool
    {
        private readonly string _folderPath;
        private readonly IReadOnlyList<string>? _fixedPictures;
        private readonly object _gate = new();
        private readonly Dictionary<string, string> _assignments =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<string>? _pictures;
        private int _nextIndex;
        private int _reservedFingerprint = int.MinValue;

        public FolderPicturePool(string folderPath)
            : this(folderPath, fixedPictures: null)
        {
        }

        public FolderPicturePool(string folderPath, IReadOnlyList<string>? fixedPictures)
        {
            _folderPath = folderPath;
            _fixedPictures = fixedPictures;
        }

        public int PictureCount
        {
            get
            {
                EnsurePicturesLoaded();
                return _pictures!.Count;
            }
        }

        public void ClearAssignment(string itemKey)
        {
            lock (_gate)
                _assignments.Remove(itemKey);
        }

        public void ClearAssignments()
        {
            lock (_gate)
            {
                _assignments.Clear();
                _nextIndex = 0;
            }
        }

        public void InvalidateSharedPictureCache()
        {
            if (_fixedPictures is not null)
                return;

            lock (_gate)
            {
                _pictures = null;
                _reservedFingerprint = int.MinValue;
            }
        }

        public string? Assign(string itemKey)
        {
            lock (_gate)
            {
                EnsurePicturesLoaded();
                if (_pictures!.Count == 0)
                    return null;

                if (_assignments.TryGetValue(itemKey, out var existing))
                    return existing;

                var path = TakeNextDistinct();
                _assignments[itemKey] = path;
                return path;
            }
        }

        public void AssignDistinct(IReadOnlyList<string> itemKeys)
        {
            if (itemKeys.Count == 0)
                return;

            lock (_gate)
            {
                EnsurePicturesLoaded();

                var pendingShared = new List<string>();
                foreach (var key in itemKeys)
                {
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    if (TryAssignDedicatedUnlocked(key))
                        continue;

                    if (!_assignments.ContainsKey(key))
                        pendingShared.Add(key);
                }

                if (pendingShared.Count == 0 || _pictures!.Count == 0)
                    return;

                // Primero fotos aún no usadas por ningún ítem de esta carpeta.
                var used = _assignments.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unused = _pictures
                    .Where(path => !used.Contains(path))
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                var queue = new Queue<string>(unused);

                // Si faltan, rellenar ciclando el set completo (mezclado).
                if (queue.Count < pendingShared.Count)
                {
                    var refill = _pictures.OrderBy(_ => Random.Shared.Next()).ToList();
                    foreach (var path in refill)
                        queue.Enqueue(path);

                    while (queue.Count < pendingShared.Count)
                    {
                        foreach (var path in refill)
                            queue.Enqueue(path);
                    }
                }

                foreach (var key in pendingShared)
                    _assignments[key] = queue.Dequeue();

                _nextIndex = _assignments.Count % Math.Max(1, _pictures.Count);
            }
        }

        private bool TryAssignDedicatedUnlocked(string itemKey)
        {
            string normalizedKey;
            try
            {
                normalizedKey = Path.GetFullPath(itemKey);
            }
            catch (Exception)
            {
                return false;
            }

            if (!DedicatedPictures.TryGetValue(normalizedKey, out var pictures) || pictures.Count == 0)
                return false;

            var dedicatedPool = Pools.GetOrAdd(
                DedicatedPoolKey(normalizedKey),
                _ => new FolderPicturePool(normalizedKey, pictures));

            // Evitar deadlock: el pool dedicado es distinto; Assign toma su propio lock.
            // Si ya estamos dentro del lock del pool compartido, liberamos el trabajo al otro pool.
            dedicatedPool.Assign(normalizedKey);
            return true;
        }

        private string TakeNextDistinct()
        {
            var used = _assignments.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _pictures!)
            {
                if (!used.Contains(path))
                    return path;
            }

            var index = _nextIndex % _pictures.Count;
            _nextIndex++;
            return _pictures[index];
        }

        private void EnsurePicturesLoaded()
        {
            if (_fixedPictures is not null)
            {
                _pictures ??= _fixedPictures
                    .OrderBy(_ => Random.Shared.Next())
                    .ToArray();
                return;
            }

            var reserved = GetReservedPicturePaths();
            var fingerprint = reserved.Count;
            foreach (var path in reserved)
                fingerprint = HashCode.Combine(fingerprint, StringComparer.OrdinalIgnoreCase.GetHashCode(path));

            if (_pictures is not null && fingerprint == _reservedFingerprint)
                return;

            _pictures = EnumeratePictures(_folderPath, reserved);
            _reservedFingerprint = fingerprint;

            // Si el pool compartido cambió (nuevas exclusiones), invalidar asignaciones
            // que apunten a rutas ya reservadas para un video concreto.
            if (reserved.Count > 0)
            {
                foreach (var pair in _assignments.ToList())
                {
                    if (reserved.Contains(pair.Value))
                        _assignments.Remove(pair.Key);
                }
            }
        }

        private static IReadOnlyList<string> EnumeratePictures(
            string folderPath,
            IReadOnlySet<string> reserved)
        {
            var picturesDirectory = Path.Combine(folderPath, "Pictures");
            if (!Directory.Exists(picturesDirectory))
                return [];

            try
            {
                return Directory.EnumerateFiles(picturesDirectory)
                    .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                    .Select(Path.GetFullPath)
                    .Where(path => !reserved.Contains(path))
                    .OrderBy(_ => Random.Shared.Next())
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }
}
