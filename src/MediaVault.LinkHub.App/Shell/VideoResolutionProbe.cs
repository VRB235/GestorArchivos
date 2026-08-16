using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;

using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Lee ancho/alto de video vía propiedades del Shell de Windows (sin decodificar fotogramas).
/// </summary>
internal static class VideoResolutionProbe
{
    private static readonly Guid VideoPropertyFormatId = new("64440491-4C8B-11D1-8B70-080036B11A03");
    private static readonly PROPERTYKEY FrameWidthKey = new(VideoPropertyFormatId, 3);
    private static readonly PROPERTYKEY FrameHeightKey = new(VideoPropertyFormatId, 4);

    private static readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, string? Label)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string? TryGetResolutionLabel(string? path)
    {
        if (!OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(path)
            || !MediaFileExtensions.IsVideo(path)
            || !File.Exists(path))
        {
            return null;
        }

        DateTime lastWriteUtc;
        try
        {
            lastWriteUtc = File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return null;
        }

        if (Cache.TryGetValue(path, out var cached) && cached.LastWriteUtc == lastWriteUtc)
            return cached.Label;

        string? label = null;
        try
        {
            var (width, height) = TryReadFrameSize(path);
            if (width > 0 && height > 0)
                label = $"{width}×{height}";
        }
        catch
        {
            label = null;
        }

        Cache[path] = (lastWriteUtc, label);
        return label;
    }

    public static Task<string?> TryGetResolutionLabelAsync(string? path) =>
        Task.Run(() => TryGetResolutionLabel(path));

    private static (uint Width, uint Height) TryReadFrameSize(string path)
    {
        var iid = typeof(IPropertyStore).GUID;
        var hr = SHGetPropertyStoreFromParsingName(
            path,
            IntPtr.Zero,
            GETPROPERTYSTOREFLAGS.GPS_DEFAULT,
            ref iid,
            out var store);

        if (hr != 0 || store is null)
            return (0, 0);

        try
        {
            var width = ReadUInt32(store, FrameWidthKey);
            var height = ReadUInt32(store, FrameHeightKey);
            return (width, height);
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static uint ReadUInt32(IPropertyStore store, PROPERTYKEY key)
    {
        var hr = store.GetValue(ref key, out var value);
        if (hr != 0)
            return 0;

        try
        {
            return value.vt switch
            {
                (ushort)VarEnum.VT_UI4 => value.ulVal,
                (ushort)VarEnum.VT_I4 => (uint)Math.Max(0, value.lVal),
                (ushort)VarEnum.VT_UI8 => (uint)Math.Min(uint.MaxValue, value.uhVal),
                _ => 0
            };
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHGetPropertyStoreFromParsingName(
        string pszPath,
        IntPtr pbc,
        GETPROPERTYSTOREFLAGS flags,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

    [DllImport("ole32.dll", ExactSpelling = true)]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    private enum GETPROPERTYSTOREFLAGS
    {
        GPS_DEFAULT = 0
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private readonly struct PROPERTYKEY(Guid formatId, uint propertyId)
    {
        public readonly Guid fmtid = formatId;
        public readonly uint pid = propertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public int lVal;
        [FieldOffset(8)] public uint ulVal;
        [FieldOffset(8)] public ulong uhVal;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint cProps);

        [PreserveSig]
        int GetAt(uint iProp, out PROPERTYKEY pkey);

        [PreserveSig]
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);

        [PreserveSig]
        int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);

        [PreserveSig]
        int Commit();
    }
}
