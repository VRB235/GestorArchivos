using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Obtiene miniaturas mediante IThumbnailCache (misma API que el Explorador de Windows).
/// </summary>
internal static class WindowsThumbnailCacheProvider
{
    private const uint ExplorerThumbnailSize = 256;

    [Flags]
    private enum WtsFlags : uint
    {
        Default = 0,
        Extract = 0x1,
        FastExtract = 0x2,
        ForceExtraction = 0x4,
        SlowRenderCache = 0x8,
        WideThumbnails = 0x10,
        ScaleBitmap = 0x20,
        ExtractDoNotCache = 0x40
    }

    [Flags]
    private enum WtsCacheFlags : uint
    {
        Default = 0,
        EntryValid = 0x1,
        FastOnly = 0x2,
        SlowOnly = 0x4,
        Refresh = 0x8
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

    public static ImageSource? TryGetVideoThumbnail(string path, int targetSize)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var shellItemGuid = typeof(IShellItem).GUID;
            var createResult = SHCreateItemFromParsingName(
                Path.GetFullPath(path),
                IntPtr.Zero,
                ref shellItemGuid,
                out var shellItem);

            if (createResult != 0 || shellItem is null)
                return null;

            foreach (var requestSize in new[] { ExplorerThumbnailSize, (uint)targetSize, 128u, 512u }.Distinct())
            {
                var image = TryGetFromThumbnailCache(shellItem, requestSize, WtsFlags.Extract | WtsFlags.ScaleBitmap);
                if (image is not null)
                    return image;
            }

            foreach (var requestSize in new[] { ExplorerThumbnailSize, (uint)targetSize, 256u }.Distinct())
            {
                var image = TryGetFromThumbnailCache(
                    shellItem,
                    requestSize,
                    WtsFlags.Extract | WtsFlags.ForceExtraction | WtsFlags.ScaleBitmap);

                if (image is not null)
                    return image;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? TryGetFromThumbnailCache(IShellItem shellItem, uint size, WtsFlags flags)
    {
        IThumbnailCache? cache = null;

        try
        {
            cache = (IThumbnailCache)new LocalThumbnailCache();
            var result = cache.GetThumbnail(
                shellItem,
                size,
                flags,
                out var sharedBitmap,
                out _,
                out _);

            if (result != 0 || sharedBitmap is null)
                return null;

            sharedBitmap.GetSharedBitmap(out var bitmapHandle);
            return CreateImageFromHBitmap(bitmapHandle);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (cache is not null)
                Marshal.ReleaseComObject(cache);
        }
    }

    private static ImageSource? CreateImageFromHBitmap(IntPtr bitmapHandle)
    {
        if (bitmapHandle == IntPtr.Zero)
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

    [ComImport]
    [Guid("8408eb4a-4b47-4668-97be-77b647ffa479")]
    internal class LocalThumbnailCache;

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem;

    [ComImport]
    [Guid("f5eb242a-4de9-4858-9444-925a3f46bdb9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IThumbnailCache
    {
        [PreserveSig]
        int GetThumbnail(
            IShellItem shellItem,
            uint thumbnailSize,
            WtsFlags flags,
            out ISharedBitmap sharedBitmap,
            out WtsCacheFlags cacheFlags,
            out IntPtr thumbnailId);

        [PreserveSig]
        int GetThumbnailByID(
            IntPtr thumbnailId,
            uint thumbnailSize,
            WtsFlags flags,
            out ISharedBitmap sharedBitmap,
            out WtsCacheFlags cacheFlags);

        [PreserveSig]
        int RegisterOverlay(
            IntPtr thumbnailId,
            IShellItem shellItem,
            uint thumbnailSize,
            WtsFlags flags);

        [PreserveSig]
        int UnregisterOverlay(
            IntPtr thumbnailId,
            IShellItem shellItem);

        [PreserveSig]
        int SetThumbnailSize(uint thumbnailSize);

        [PreserveSig]
        int GetThumbnailSize(out uint thumbnailSize);
    }

    [ComImport]
    [Guid("67774f99-8e35-4c55-876b-cb29d681c38e")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISharedBitmap
    {
        void GetSharedBitmap(out IntPtr phbm);

        void GetSize(out NativeSize pSize);

        void GetFormat(out Guid guidFormat);

        void CopyTo(ISharedBitmap destination);

        void InitializeFromBitmap(IntPtr hBitmap, WtsAlphaType alphaType);

        void Detach(out IntPtr phbm);

        void GetSurfaceSize(out NativeSize pSize);

        void Draw(IntPtr hSurface, NativePoint offset);

        void GetColorContext(out IntPtr colorContext);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize(int cx, int cy)
    {
        public int cx = cx;
        public int cy = cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint(int x, int y)
    {
        public int x = x;
        public int y = y;
    }

    private enum WtsAlphaType
    {
        Unknown = 0,
        Premultiplied = 1,
        Straight = 2
    }
}
