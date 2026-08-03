using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Obtiene iconos y miniaturas del Shell de Windows (mismo mecanismo que el Explorador).
/// </summary>
internal static class WindowsShellThumbnailProvider
{
    private const int ExplorerThumbnailSize = 256;

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int cx, int cy)
    {
        public int cx = cx;
        public int cy = cy;
    }

    [Flags]
    private enum ShellImageFlags
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        MemoryOnly = 0x02,
        IconOnly = 0x04,
        ThumbnailOnly = 0x08,
        InCacheOnly = 0x10
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem;

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c963b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(NativeSize size, ShellImageFlags flags, out IntPtr bitmapHandle);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    public static Task<ImageSource?> GetThumbnailAsync(string path, bool isDirectory, int size = 128)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path))
            return Task.FromResult<ImageSource?>(null);

        var cacheKey = BuildCacheKey(path, isDirectory, size);
        if (Cache.TryGetValue(cacheKey, out var cached))
            return Task.FromResult(cached);

        if (MediaFileExtensions.IsVideo(path) && !isDirectory)
            return LoadVideoThumbnailAsync(path, size, cacheKey);

        return StaShellThumbnailExecutor.RunAsync(() =>
            isDirectory
                ? LoadDirectoryThumbnail(path, size, cacheKey)
                : LoadFileThumbnail(path, size, cacheKey));
    }

    public static ImageSource? GetThumbnail(string path, bool isDirectory, int size = 128) =>
        GetThumbnailAsync(path, isDirectory, size).GetAwaiter().GetResult();

    public static void ClearCache() => Cache.Clear();

    public static void InvalidateCacheEntry(string path, bool isDirectory, int size = 128)
    {
        Cache.TryRemove(BuildCacheKey(path, isDirectory, size), out _);
        Cache.TryRemove(BuildCacheKey(path, isDirectory, 256), out _);
    }

    private static async Task<ImageSource?> LoadVideoThumbnailAsync(string path, int size, string cacheKey)
    {
        var image = await StaShellThumbnailExecutor
            .RunAsync(() => TryLoadShellVideoThumbnail(path, size))
            .ConfigureAwait(false);

        if (image is not null)
            Store(cacheKey, image);

        return image;
    }

    /// <summary>
    /// Solo fuentes del Shell de Windows; nunca sustitutos propios (WPF/VLC).
    /// </summary>
    private static ImageSource? TryLoadShellVideoThumbnail(string path, int size)
    {
        if (!File.Exists(path))
            return null;

        var fromCache = WindowsThumbnailCacheProvider.TryGetVideoThumbnail(path, size);
        if (fromCache is not null)
            return fromCache;

        try
        {
            var shellItemGuid = typeof(IShellItem).GUID;
            var createResult = SHCreateItemFromParsingName(
                Path.GetFullPath(path),
                IntPtr.Zero,
                ref shellItemGuid,
                out var shellItem);

            if (createResult != 0 || shellItem is not IShellItemImageFactory factory)
                return null;

            foreach (var requestSize in new[] { ExplorerThumbnailSize, size, 128, 512 }.Distinct())
            {
                var shellSize = new NativeSize(requestSize, requestSize);

                var cached = GetShellBitmap(
                    factory,
                    shellSize,
                    ShellImageFlags.ThumbnailOnly | ShellImageFlags.InCacheOnly | ShellImageFlags.BiggerSizeOk);

                if (cached is not null)
                    return cached;
            }

            foreach (var requestSize in new[] { ExplorerThumbnailSize, size, 256 }.Distinct())
            {
                var shellSize = new NativeSize(requestSize, requestSize);

                var thumbnail = GetShellBitmap(
                    factory,
                    shellSize,
                    ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk);

                if (thumbnail is not null)
                    return thumbnail;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadFileThumbnail(string path, int size, string cacheKey)
    {
        var image = TryLoadShellThumbnail(path, isDirectory: false, size) ?? TryLoadImagePreview(path, size);
        Store(cacheKey, image);
        return image;
    }

    private static ImageSource? LoadDirectoryThumbnail(string path, int size, string cacheKey)
    {
        var image = TryLoadShellThumbnail(path, isDirectory: true, size);
        Store(cacheKey, image);
        return image;
    }

    private static void Store(string cacheKey, ImageSource? image)
    {
        if (image is null)
            return;

        image.Freeze();
        Cache[cacheKey] = image;
    }

    private static string BuildCacheKey(string path, bool isDirectory, int size) =>
        $"{size}|{(isDirectory ? "dir" : "file")}|{path}";

    private static ImageSource? TryLoadShellThumbnail(string path, bool isDirectory, int size)
    {
        if (isDirectory)
        {
            if (!Directory.Exists(path))
                return null;
        }
        else if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var shellItemGuid = typeof(IShellItem).GUID;
            var createResult = SHCreateItemFromParsingName(
                Path.GetFullPath(path),
                IntPtr.Zero,
                ref shellItemGuid,
                out var shellItem);

            if (createResult == 0 && shellItem is IShellItemImageFactory factory)
                return TryGetShellImage(factory, size, isDirectory);
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static ImageSource? TryGetShellImage(IShellItemImageFactory factory, int size, bool isDirectory)
    {
        var shellSize = new NativeSize(size, size);

        if (isDirectory)
            return GetShellBitmap(factory, shellSize, ShellImageFlags.IconOnly | ShellImageFlags.BiggerSizeOk);

        return GetShellBitmap(factory, shellSize, ShellImageFlags.ThumbnailOnly | ShellImageFlags.BiggerSizeOk)
            ?? GetShellBitmap(factory, shellSize, ShellImageFlags.BiggerSizeOk);
    }

    private static ImageSource? GetShellBitmap(
        IShellItemImageFactory factory,
        NativeSize size,
        ShellImageFlags flags)
    {
        var result = factory.GetImage(size, flags, out var bitmapHandle);
        if (result != 0 || bitmapHandle == IntPtr.Zero)
            return null;

        try
        {
            return Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(bitmapHandle);
        }
    }

    private static ImageSource? TryLoadImagePreview(string path, int size)
    {
        var extension = Path.GetExtension(path);
        if (!IsRasterImageExtension(extension))
            return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = size;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsRasterImageExtension(string extension) =>
        extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase);
}
