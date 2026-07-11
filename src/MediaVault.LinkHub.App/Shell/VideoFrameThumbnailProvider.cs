using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Extrae un fotograma de video mediante Media Foundation (Windows).
/// </summary>
internal static class VideoFrameThumbnailProvider
{
    private const int MfVersion = (0x0002 << 16) | 0x0070;
    private const int MfSourceReaderFirstVideoStream = unchecked((int)0xFFFFFFFC);
    private const int MfSourceReaderControlFrameRead = 0x00000001;
    private const int MfSourceReaderFEndOfStream = 0x00000001;
    private const int MaxReadAttempts = 40;

    private static readonly Guid MfMediaTypeVideo = new("73646976-0000-0010-8000-00AA00389B71");
    private static readonly Guid MfVideoFormatRgb32 = new("00000022-0000-0010-8000-00AA00389B71");
    private static readonly Guid MfMtMajorType = new("48eba18e-f8c9-468a-bf14-b8fe5f560f6a");
    private static readonly Guid MfMtSubtype = new("f7e34c9a-42e8-4714-b74b-cb29d681c38e");
    private static readonly Guid MfMtFrameSize = new("1652c33d-f6a3-4356-903a-3a2ba2ac6a9b");

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object StartupLock = new();
    private static int _startupCount;

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFStartup(int version, int dwFlags);

    [DllImport("mfreadwrite.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MFCreateSourceReaderFromURL(
        string url,
        IntPtr attributes,
        out IMFSourceReader sourceReader);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    private static extern int MFCreateMediaType(out IMFMediaType mediaType);

    public static ImageSource? TryGetThumbnail(string path, int targetSize)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        if (!MediaFileExtensions.IsVideo(path))
            return null;

        var cacheKey = $"{targetSize}|{path}";
        if (Cache.TryGetValue(cacheKey, out var cached))
            return cached;

        ImageSource? image = null;

        try
        {
            EnsureStarted();
            image = ExtractFrame(path, targetSize);
            image?.Freeze();
        }
        catch
        {
            image = null;
        }

        Cache[cacheKey] = image;
        return image;
    }

    public static void ClearCache() => Cache.Clear();

    private static void EnsureStarted()
    {
        lock (StartupLock)
        {
            if (_startupCount++ == 0)
            {
                var result = MFStartup(MfVersion, 0);
                if (result != 0)
                    throw new InvalidOperationException($"MFStartup failed: 0x{result:X8}");
            }
        }
    }

    private static ImageSource? ExtractFrame(string path, int targetSize)
    {
        foreach (var source in BuildSourceCandidates(path))
        {
            var image = TryExtractFromSource(source, targetSize);
            if (image is not null)
                return image;
        }

        return null;
    }

    private static IEnumerable<string> BuildSourceCandidates(string path)
    {
        yield return Path.GetFullPath(path);
        yield return new Uri(Path.GetFullPath(path)).AbsoluteUri;
    }

    private static ImageSource? TryExtractFromSource(string source, int targetSize)
    {
        var createResult = MFCreateSourceReaderFromURL(source, IntPtr.Zero, out var reader);
        if (createResult != 0 || reader is null)
            return null;

        try
        {
            var mediaTypeResult = MFCreateMediaType(out var outputType);
            if (mediaTypeResult != 0 || outputType is null)
                return null;

            outputType.SetGuid(MfMtMajorType, MfMediaTypeVideo);
            outputType.SetGuid(MfMtSubtype, MfVideoFormatRgb32);

            var setTypeResult = reader.SetCurrentMediaType(
                MfSourceReaderFirstVideoStream,
                IntPtr.Zero,
                outputType);

            if (setTypeResult != 0)
                return null;

            reader.GetCurrentMediaType(MfSourceReaderFirstVideoStream, out var currentType);
            if (currentType is null)
                return null;

            currentType.GetUInt64(MfMtFrameSize, out var frameSize);
            var width = (int)(frameSize & 0xFFFFFFFF);
            var height = (int)(frameSize >> 32);

            if (width <= 0 || height <= 0)
                return null;

            for (var attempt = 0; attempt < MaxReadAttempts; attempt++)
            {
                var readResult = reader.ReadSample(
                    MfSourceReaderFirstVideoStream,
                    MfSourceReaderControlFrameRead,
                    out _,
                    out var streamFlags,
                    out _,
                    out var sample);

                if (readResult != 0 || sample is null)
                    continue;

                if ((streamFlags & MfSourceReaderFEndOfStream) != 0)
                    break;

                try
                {
                    sample.ConvertToContiguousBuffer(out var buffer);
                    if (buffer is null)
                        continue;

                    buffer.Lock(out var dataPtr, out _, out var currentLength);
                    try
                    {
                        var stride = width * 4;
                        var expectedLength = stride * height;
                        if (currentLength < expectedLength)
                            continue;

                        var pixels = new byte[expectedLength];
                        Marshal.Copy(dataPtr, pixels, 0, expectedLength);
                        return CreateScaledBitmap(pixels, width, height, stride, targetSize);
                    }
                    finally
                    {
                        buffer.Unlock();
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(sample);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(reader);
        }

        return null;
    }

    private static BitmapSource CreateScaledBitmap(
        byte[] pixels,
        int width,
        int height,
        int stride,
        int targetSize)
    {
        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Bgr32,
            null,
            pixels,
            stride);

        if (width <= targetSize && height <= targetSize)
            return bitmap;

        var scale = Math.Min((double)targetSize / width, (double)targetSize / height);
        return new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
    }

    [ComImport]
    [Guid("70ae66f2-c809-4e7f-8957-491a7886a65b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSourceReader
    {
        [PreserveSig]
        int GetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] out bool selected);

        [PreserveSig]
        int SetStreamSelection(int streamIndex, [MarshalAs(UnmanagedType.Bool)] bool selected);

        [PreserveSig]
        int GetNativeMediaType(int streamIndex, int mediaTypeIndex, out IMFMediaType mediaType);

        [PreserveSig]
        int GetCurrentMediaType(int streamIndex, out IMFMediaType mediaType);

        [PreserveSig]
        int SetCurrentMediaType(int streamIndex, IntPtr reserved, IMFMediaType mediaType);

        [PreserveSig]
        int ReadSample(
            int streamIndex,
            int controlFlags,
            out int actualStreamIndex,
            out int streamFlags,
            out long timestamp,
            out IMFSample sample);

        [PreserveSig]
        int Flush(int streamIndex);

        [PreserveSig]
        int GetServiceForStream(int streamIndex, ref Guid service, ref Guid riid, out IntPtr serviceObject);

        [PreserveSig]
        int NotifyEndOfPresentation(int status);
    }

    [ComImport]
    [Guid("44ae0fa8-ea31-4109-8d2e-4cae4997a28a")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaType
    {
        [PreserveSig]
        int GetItem(Guid key, out IntPtr value);

        [PreserveSig]
        int GetItemType(Guid key, out int itemType);

        [PreserveSig]
        int CompareItem(Guid key, IntPtr value, [MarshalAs(UnmanagedType.Bool)] out bool equal);

        [PreserveSig]
        int Compare(IMFMediaType attributes, int matchType, [MarshalAs(UnmanagedType.Bool)] out bool equal);

        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int GetItemByIndex(int index, out Guid key, out IntPtr value);

        [PreserveSig]
        int CopyAllItems(IMFMediaType destination);

        [PreserveSig]
        int DeleteItem(Guid key);

        [PreserveSig]
        int DeleteAllItems();

        [PreserveSig]
        int SetUInt32(Guid key, int value);

        [PreserveSig]
        int SetUInt64(Guid key, ulong value);

        [PreserveSig]
        int SetDouble(Guid key, double value);

        [PreserveSig]
        int SetGuid(Guid key, Guid value);

        [PreserveSig]
        int SetString(Guid key, string value);

        [PreserveSig]
        int SetBlob(Guid key, byte[] buffer, int bufferSize);

        [PreserveSig]
        int SetUnknown(Guid key, [MarshalAs(UnmanagedType.IUnknown)] object unknown);

        [PreserveSig]
        int LockStore();

        [PreserveSig]
        int UnlockStore();

        [PreserveSig]
        int GetUInt32(Guid key, out int value);

        [PreserveSig]
        int GetUInt64(Guid key, out ulong value);

        [PreserveSig]
        int GetDouble(Guid key, out double value);

        [PreserveSig]
        int GetGuid(Guid key, out Guid value);

        [PreserveSig]
        int GetStringLength(Guid key, out int length);

        [PreserveSig]
        int GetString(Guid key, [Out] char[] value, int capacity, out int length);

        [PreserveSig]
        int GetBlobSize(Guid key, out int blobSize);

        [PreserveSig]
        int GetBlob(Guid key, [Out] byte[] buffer, int bufferSize, out int blobSize);

        [PreserveSig]
        int GetAllocatedBlob(Guid key, out IntPtr buffer, out int size);

        [PreserveSig]
        int GetUnknown(Guid key, ref Guid riid, out IntPtr unknown);

        [PreserveSig]
        int SetItem(Guid key, IntPtr value);

        [PreserveSig]
        int GetMajorType(out Guid majorType);

        [PreserveSig]
        int IsCompressedFormat([MarshalAs(UnmanagedType.Bool)] out bool compressed);

        [PreserveSig]
        int IsEqual(IMFMediaType mediaType, out int flags);
    }

    [ComImport]
    [Guid("2cd2d921-c447-44a7-a13c-4fadb82340f0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFSample
    {
        [PreserveSig]
        int GetSampleFlags(out int sampleFlags);

        [PreserveSig]
        int SetSampleFlags(int sampleFlags);

        [PreserveSig]
        int GetSampleTime(out long sampleTime);

        [PreserveSig]
        int SetSampleTime(long sampleTime);

        [PreserveSig]
        int GetSampleDuration(out long sampleDuration);

        [PreserveSig]
        int SetSampleDuration(long sampleDuration);

        [PreserveSig]
        int GetBufferCount(out int bufferCount);

        [PreserveSig]
        int GetBufferByIndex(int index, out IMFMediaBuffer buffer);

        [PreserveSig]
        int ConvertToContiguousBuffer(out IMFMediaBuffer buffer);

        [PreserveSig]
        int AddBuffer(IMFMediaBuffer buffer);

        [PreserveSig]
        int RemoveBufferByIndex(int index);

        [PreserveSig]
        int RemoveAllBuffers();

        [PreserveSig]
        int GetTotalLength(out int totalLength);

        [PreserveSig]
        int CopyToBuffer(IMFMediaBuffer buffer);
    }

    [ComImport]
    [Guid("045fa593-8799-42b8-bc8d-8958cc3671c7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFMediaBuffer
    {
        [PreserveSig]
        int Lock(out IntPtr buffer, out int maxLength, out int currentLength);

        [PreserveSig]
        int Unlock();

        [PreserveSig]
        int GetCurrentLength(out int currentLength);

        [PreserveSig]
        int SetCurrentLength(int currentLength);

        [PreserveSig]
        int GetMaxLength(out int maxLength);
    }
}
