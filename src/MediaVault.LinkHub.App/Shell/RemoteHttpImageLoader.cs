using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Descarga miniaturas HTTP en formatos que WPF (<see cref="BitmapImage"/>) sí puede decodificar.
/// Evita AVIF/WebP que muchos CDN entregan con <c>format=auto</c> / Accept moderno.
/// </summary>
public static class RemoteHttpImageLoader
{
    /// <summary>Accept sin avif/webp para que el CDN (p. ej. Cloudflare) sirva JPEG/PNG.</summary>
    public const string WpfFriendlyAccept =
        "image/jpeg,image/jpg,image/png,image/gif,image/bmp,image/*;q=0.5,*/*;q=0.1";

    public static Uri PreferWpfDecodableUri(Uri uri)
    {
        var absolute = uri.AbsoluteUri;
        if (!absolute.Contains("format=auto", StringComparison.OrdinalIgnoreCase))
            return uri;

        var rewritten = Regex.Replace(absolute, "format=auto", "format=jpeg", RegexOptions.IgnoreCase);
        return Uri.TryCreate(rewritten, UriKind.Absolute, out var result) ? result : uri;
    }

    public static async Task<BitmapImage?> TryLoadAsync(
        HttpClient client,
        Uri imageUri,
        Uri? referrer,
        int decodePixelWidth,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(imageUri);

        var requestUri = PreferWpfDecodableUri(imageUri);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.TryAddWithoutValidation("Accept", WpfFriendlyAccept);
        if (referrer is not null)
            request.Headers.Referrer = referrer;

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length == 0)
            return null;

        return await Task.Run(() => TryDecode(bytes, decodePixelWidth), cancellationToken).ConfigureAwait(false);
    }

    public static BitmapImage? TryDecode(byte[] bytes, int decodePixelWidth)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.StreamSource = stream;
            if (decodePixelWidth > 0)
                bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (FileFormatException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
