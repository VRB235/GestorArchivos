using System.IO;
using System.Windows;
using System.Windows.Controls;

using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MediaVault.LinkHub.App.Scraping;

/// <summary>
/// Obtiene HTML vía WebView2 (cookies, JS, age-gate interactivo) con perfil persistente.
/// </summary>
public sealed class WebView2HtmlFetcher : IBrowserHtmlFetcher
{
    private static readonly string UserDataFolder = Path.Combine(
        SqliteDatabasePathProvider.GetAppDataDirectory(),
        "WebView2ScrapeProfile");

    public async Task<string> FetchHtmlAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = await FetchAsync(url, progress, cancellationToken).ConfigureAwait(false);
        return result.Html;
    }

    public async Task<BrowserHtmlFetchResult> FetchAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await using var session = await BeginSessionAsync(cancellationToken).ConfigureAwait(false);
        return await session.NavigateAsync(url, progress, cancellationToken).ConfigureAwait(false);
    }

    public Task<IBrowserHtmlSession> BeginSessionAsync(CancellationToken cancellationToken = default)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("No hay Dispatcher de WPF disponible.");

        return dispatcher.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(UserDataFolder);

            var host = new Window
            {
                Title = "Scrape en navegador — complete age-gate/login si aparece",
                Width = 1100,
                Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow
            };

            var root = new DockPanel();
            var status = new TextBlock
            {
                Margin = new Thickness(12),
                TextWrapping = TextWrapping.Wrap,
                Text = "Inicializando WebView2…"
            };
            DockPanel.SetDock(status, Dock.Top);
            root.Children.Add(status);

            var webView = new WebView2();
            root.Children.Add(webView);
            host.Content = root;
            host.Show();

            try
            {
                status.Text = "Iniciando WebView2 (perfil persistente)…";
                var environment = await CoreWebView2Environment
                    .CreateAsync(userDataFolder: UserDataFolder)
                    .ConfigureAwait(true);

                try
                {
                    await webView.EnsureCoreWebView2Async(environment).ConfigureAwait(true);
                }
                catch (WebView2RuntimeNotFoundException ex)
                {
                    host.Close();
                    throw new InvalidOperationException(
                        "WebView2 Runtime no está instalado. Instálelo o use sitios que no bloqueen HttpClient.",
                        ex);
                }

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = true;

                return (IBrowserHtmlSession)new WebView2HtmlSession(host, webView, status);
            }
            catch
            {
                try { host.Close(); } catch { /* ignore */ }
                throw;
            }
        }).Task.Unwrap();
    }

    private sealed class WebView2HtmlSession : IBrowserHtmlSession
    {
        private readonly Window _host;
        private readonly WebView2 _webView;
        private readonly TextBlock _status;
        private bool _disposed;

        public WebView2HtmlSession(Window host, WebView2 webView, TextBlock status)
        {
            _host = host;
            _webView = webView;
            _status = status;
        }

        public async Task<BrowserHtmlFetchResult> NavigateAsync(
            string url,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            var dispatcher = _host.Dispatcher;
            return await dispatcher.InvokeAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                void Report(string message)
                {
                    progress?.Report(message);
                    _status.Text = message;
                }

                var navTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnNav(object? sender, CoreWebView2NavigationCompletedEventArgs args)
                {
                    if (args.IsSuccess)
                        navTcs.TrySetResult(true);
                    else
                        navTcs.TrySetException(
                            new InvalidOperationException(
                                $"Navegación WebView2 falló (código {args.WebErrorStatus})."));
                }

                _webView.CoreWebView2.NavigationCompleted += OnNav;
                try
                {
                    Report($"Cargando {url} …");
                    _webView.CoreWebView2.Navigate(url);

                    using var reg = cancellationToken.Register(() => navTcs.TrySetCanceled(cancellationToken));
                    await navTcs.Task.ConfigureAwait(true);
                }
                finally
                {
                    _webView.CoreWebView2.NavigationCompleted -= OnNav;
                }

                Report("Esperando render JS / age-gate (8 s). Complete el challenge en la ventana si aparece…");
                await Task.Delay(8000, cancellationToken).ConfigureAwait(true);

                string html = string.Empty;
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    html = await ReadDocumentHtmlAsync(_webView).ConfigureAwait(true);
                    if (html.Length > 2_000 && !LooksLikeBlockPage(html))
                    {
                        Report($"HTML obtenido ({html.Length:N0} chars).");
                        break;
                    }

                    Report(
                        attempt == 0
                            ? "HTML aún incompleto o bloqueado. Espere o complete el age-gate… (8 s más)"
                            : $"Reintento {attempt + 1}/3…");
                    await Task.Delay(8000, cancellationToken).ConfigureAwait(true);
                    html = await ReadDocumentHtmlAsync(_webView).ConfigureAwait(true);
                }

                if (html.Length < 200)
                {
                    throw new InvalidOperationException(
                        "WebView2 no devolvió HTML útil. Complete el age-gate y vuelva a scrapear.");
                }

                Report($"HTML final ({html.Length:N0} chars). Extrayendo cookies…");
                var cookies = await ReadCookiesAsync(_webView, url).ConfigureAwait(true);
                var finalUrl = _webView.CoreWebView2.Source;

                return new BrowserHtmlFetchResult
                {
                    Html = html,
                    Cookies = cookies,
                    FinalUrl = finalUrl
                };
            }).Task.Unwrap();
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return ValueTask.CompletedTask;

            _disposed = true;
            try
            {
                _host.Dispatcher.Invoke(() =>
                {
                    try { _host.Close(); } catch { /* ignore */ }
                });
            }
            catch
            {
                // ignore
            }

            return ValueTask.CompletedTask;
        }
    }

    private static async Task<string> ReadDocumentHtmlAsync(WebView2 webView)
    {
        var raw = await webView.CoreWebView2
            .ExecuteScriptAsync("document.documentElement ? document.documentElement.outerHTML : ''")
            .ConfigureAwait(true);

        return DecodeScriptString(raw);
    }

    private static async Task<IReadOnlyList<BrowserCookie>> ReadCookiesAsync(WebView2 webView, string url)
    {
        try
        {
            var cookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(url).ConfigureAwait(true);
            return cookies.Select(static c => new BrowserCookie
            {
                Name = c.Name,
                Value = c.Value,
                Domain = c.Domain,
                Path = string.IsNullOrWhiteSpace(c.Path) ? "/" : c.Path,
                Secure = c.IsSecure,
                HttpOnly = c.IsHttpOnly,
                ExpiresUtc = c.IsSession ? null : c.Expires.ToUniversalTime()
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string DecodeScriptString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
            return string.Empty;

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
        }
        catch
        {
            return raw.Trim('"');
        }
    }

    private static bool LooksLikeBlockPage(string html)
    {
        var sample = html.Length > 4000 ? html[..4000] : html;
        return sample.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || sample.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || sample.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
            || sample.Contains("Attention Required", StringComparison.OrdinalIgnoreCase)
            || sample.Contains("cf-turnstile", StringComparison.OrdinalIgnoreCase);
    }
}
