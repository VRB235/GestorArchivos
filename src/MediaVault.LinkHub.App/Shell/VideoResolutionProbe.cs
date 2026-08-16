using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Resuelve ancho×alto de video sin COM Shell.
/// <c>SHGetPropertyStoreFromParsingName</c> provocaba AccessViolation (0xC0000005) y cerraba la app.
/// </summary>
internal static class VideoResolutionProbe
{
    private static readonly ConcurrentDictionary<string, (DateTime LastWriteUtc, string? Label)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string? TryGetResolutionLabel(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !MediaFileExtensions.IsVideo(path)
            || !MediaPathEligibility.ExistsSafely(path))
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
            if (IsIsoBmff(Path.GetExtension(path)))
            {
                var size = TryReadIsoBmffTrackSize(path);
                if (size is { Width: > 0, Height: > 0 })
                    label = $"{size.Value.Width}×{size.Value.Height}";
            }
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

    private static bool IsIsoBmff(string extension) =>
        extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase);

    private static (uint Width, uint Height)? TryReadIsoBmffTrackSize(string path)
    {
        using var stream = File.OpenRead(path);
        return WalkAtoms(stream, stream.Length, depth: 0);
    }

    private static (uint Width, uint Height)? WalkAtoms(Stream stream, long end, int depth)
    {
        if (depth > 12)
            return null;

        var header = new byte[8];
        while (stream.Position + 8 <= end)
        {
            var start = stream.Position;
            if (stream.Read(header, 0, 8) != 8)
                return null;

            long atomSize = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
            var type = Encoding.ASCII.GetString(header, 4, 4);
            long headerSize = 8;

            if (atomSize == 1)
            {
                if (stream.Position + 8 > end)
                    return null;
                var large = new byte[8];
                if (stream.Read(large, 0, 8) != 8)
                    return null;
                atomSize = (long)BinaryPrimitives.ReadUInt64BigEndian(large);
                headerSize = 16;
            }
            else if (atomSize == 0)
            {
                atomSize = end - start;
            }

            if (atomSize < headerSize)
                return null;

            var payloadStart = start + headerSize;
            var payloadEnd = start + atomSize;
            if (payloadEnd > end || payloadEnd < payloadStart)
                return null;

            if (type is "moov" or "trak" or "mdia" or "minf" or "stbl")
            {
                stream.Position = payloadStart;
                var nested = WalkAtoms(stream, payloadEnd, depth + 1);
                if (nested is not null)
                    return nested;
            }
            else if (type == "tkhd")
            {
                stream.Position = payloadStart;
                var fromTkhd = ReadTkhdSize(stream, payloadEnd - payloadStart);
                if (fromTkhd is not null)
                    return fromTkhd;
            }

            stream.Position = payloadEnd;
        }

        return null;
    }

    private static (uint Width, uint Height)? ReadTkhdSize(Stream stream, long payloadLength)
    {
        if (payloadLength < 84)
            return null;

        var version = stream.ReadByte();
        if (version < 0)
            return null;

        var widthOffset = version == 1 ? 88 : 76;
        var required = widthOffset + 8;
        if (payloadLength < required)
            return null;

        // Volver al inicio del payload y leer el bloque necesario.
        stream.Position -= 1;
        var data = new byte[required];
        if (stream.Read(data, 0, required) != required)
            return null;

        var width = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(widthOffset, 4)) >> 16;
        var height = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(widthOffset + 4, 4)) >> 16;
        if (width == 0 || height == 0)
            return null;

        return (width, height);
    }
}
