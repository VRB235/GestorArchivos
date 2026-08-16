using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Asigna fotos de <c>{carpeta}/Pictures</c> a ítems (videos) evitando repetir
/// mientras haya fotos distintas disponibles. Las asignaciones viven con el proceso.
/// </summary>
internal static class FolderSessionPicturePicker
{
    private static readonly ConcurrentDictionary<string, FolderPicturePool> Pools =
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

    private sealed class FolderPicturePool
    {
        private readonly string _folderPath;
        private readonly object _gate = new();
        private readonly Dictionary<string, string> _assignments =
            new(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<string>? _pictures;
        private int _nextIndex;

        public FolderPicturePool(string folderPath) =>
            _folderPath = folderPath;

        public int PictureCount
        {
            get
            {
                EnsurePicturesLoaded();
                return _pictures!.Count;
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
                if (_pictures!.Count == 0)
                    return;

                var pending = itemKeys
                    .Where(key => !_assignments.ContainsKey(key))
                    .ToList();

                if (pending.Count == 0)
                    return;

                // Primero fotos aún no usadas por ningún ítem de esta carpeta.
                var used = _assignments.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unused = _pictures
                    .Where(path => !used.Contains(path))
                    .OrderBy(_ => Random.Shared.Next())
                    .ToList();

                var queue = new Queue<string>(unused);

                // Si faltan, rellenar ciclando el set completo (mezclado).
                if (queue.Count < pending.Count)
                {
                    var refill = _pictures.OrderBy(_ => Random.Shared.Next()).ToList();
                    foreach (var path in refill)
                        queue.Enqueue(path);

                    while (queue.Count < pending.Count)
                    {
                        foreach (var path in refill)
                            queue.Enqueue(path);
                    }
                }

                foreach (var key in pending)
                    _assignments[key] = queue.Dequeue();

                _nextIndex = _assignments.Count % Math.Max(1, _pictures.Count);
            }
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
            if (_pictures is not null)
                return;

            _pictures = EnumeratePictures(_folderPath);
        }

        private static IReadOnlyList<string> EnumeratePictures(string folderPath)
        {
            var picturesDirectory = Path.Combine(folderPath, "Pictures");
            if (!Directory.Exists(picturesDirectory))
                return [];

            try
            {
                return Directory.EnumerateFiles(picturesDirectory)
                    .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
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
