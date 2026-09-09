using System.Net;

using MediaVault.LinkHub.Application.Models.Scraping;

namespace MediaVault.LinkHub.Infrastructure.Scraping;

/// <summary>
/// Sesión HTTP compartida del scraper: cookies propagadas desde WebView2 hacia HttpClient.
/// </summary>
public sealed class VideoScraperSession
{
    public CookieContainer CookieContainer { get; } = new();

    public void MergeCookies(IEnumerable<BrowserCookie> cookies, Uri? requestUri = null)
    {
        foreach (var cookie in cookies)
        {
            if (string.IsNullOrWhiteSpace(cookie.Name))
                continue;

            try
            {
                var domain = cookie.Domain?.Trim() ?? string.Empty;
                var path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path;
                var netCookie = new Cookie(cookie.Name.Trim(), cookie.Value ?? string.Empty, path)
                {
                    Secure = cookie.Secure,
                    HttpOnly = cookie.HttpOnly
                };

                if (cookie.ExpiresUtc is { } expires)
                    netCookie.Expires = expires.Kind == DateTimeKind.Utc
                        ? expires.ToLocalTime()
                        : expires;

                if (!string.IsNullOrWhiteSpace(domain))
                {
                    // CookieContainer espera dominio sin esquema; aceptar ".example.com".
                    netCookie.Domain = domain.StartsWith('.') ? domain : domain;
                    CookieContainer.Add(netCookie);
                }
                else if (requestUri is not null)
                {
                    CookieContainer.Add(requestUri, netCookie);
                }
            }
            catch (CookieException)
            {
                // Dominio/path inválido: intentar asociar a la URI de la petición.
                if (requestUri is null)
                    continue;

                try
                {
                    CookieContainer.Add(
                        requestUri,
                        new Cookie(cookie.Name.Trim(), cookie.Value ?? string.Empty, "/", requestUri.Host)
                        {
                            Secure = cookie.Secure,
                            HttpOnly = cookie.HttpOnly
                        });
                }
                catch (CookieException)
                {
                    // ignore cookie inválida
                }
            }
        }
    }

    public int CountCookies(Uri uri)
    {
        try
        {
            return CookieContainer.GetCookies(uri).Count;
        }
        catch
        {
            return 0;
        }
    }
}
