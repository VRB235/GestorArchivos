namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Resultado de una navegación con navegador real (HTML + cookies de la sesión).
/// </summary>
public sealed class BrowserHtmlFetchResult
{
    public required string Html { get; init; }

    public IReadOnlyList<BrowserCookie> Cookies { get; init; } = [];

    public string? FinalUrl { get; init; }
}

/// <summary>
/// Cookie exportable desde WebView2 hacia el <see cref="System.Net.CookieContainer"/> de HttpClient.
/// </summary>
public sealed class BrowserCookie
{
    public required string Name { get; init; }

    public required string Value { get; init; }

    public string Domain { get; init; } = string.Empty;

    public string Path { get; init; } = "/";

    public bool Secure { get; init; }

    public bool HttpOnly { get; init; }

    public DateTime? ExpiresUtc { get; init; }
}
