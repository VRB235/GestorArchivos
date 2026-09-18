using System.Globalization;
using System.Text.RegularExpressions;

namespace MediaVault.LinkHub.Infrastructure.Downloads;

/// <summary>
/// Parser mínimo de playlists HLS (master + media).
/// </summary>
internal static class HlsPlaylistParser
{
    private static readonly Regex BandwidthRegex = new(
        @"BANDWIDTH=(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsMasterPlaylist(string content) =>
        content.Contains("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase);

    public static bool IsEncrypted(string content) =>
        content.Contains("#EXT-X-KEY:", StringComparison.OrdinalIgnoreCase);

    public static Uri? SelectBestVariant(string content, Uri baseUri)
    {
        var lines = SplitLines(content);
        Uri? bestUri = null;
        var bestBandwidth = -1;
        int? pendingBandwidth = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase))
            {
                pendingBandwidth = 0;
                var match = BandwidthRegex.Match(line);
                if (match.Success
                    && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bw))
                {
                    pendingBandwidth = bw;
                }

                continue;
            }

            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line) || pendingBandwidth is null)
                continue;

            if (!TryResolveUri(baseUri, line, out var uri))
            {
                pendingBandwidth = null;
                continue;
            }

            if (pendingBandwidth.Value >= bestBandwidth)
            {
                bestBandwidth = pendingBandwidth.Value;
                bestUri = uri;
            }

            pendingBandwidth = null;
        }

        return bestUri;
    }

    public static IReadOnlyList<Uri> ParseSegments(string content, Uri baseUri)
    {
        var segments = new List<Uri>();
        foreach (var line in SplitLines(content))
        {
            if (line.StartsWith('#') || string.IsNullOrWhiteSpace(line))
                continue;

            if (TryResolveUri(baseUri, line, out var uri))
                segments.Add(uri);
        }

        return segments;
    }

    private static IEnumerable<string> SplitLines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryResolveUri(Uri baseUri, string value, out Uri uri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out uri!))
            return true;

        return Uri.TryCreate(baseUri, value, out uri!);
    }
}
