using System.Windows;
using System.Windows.Controls;

using MediaVault.LinkHub.Application.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MediaVault.LinkHub.App.Scraping;

/// <summary>
/// Obtiene HTML vía WebView2 (cookies, JS, age-gate interactivo).
/// </summary>
public sealed class WebView2HtmlFetcher : IBrowserHtmlFetcher
{
    public Task<string> FetchHtmlAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("No hay Dispatcher de WPF disponible.");

        return dispatcher.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            void Report(string message)
            {
                progress?.Report(message);
                status.Text = message;
            }

            try
            {
                Report("Iniciando WebView2…");
                try
                {
                    await webView.EnsureCoreWebView2Async().ConfigureAwait(true);
                }
                catch (WebView2RuntimeNotFoundException ex)
                {
                    throw new InvalidOperationException(
                        "WebView2 Runtime no está instalado. Instálelo o use sitios que no bloqueen HttpClient.",
                        ex);
                }

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = true;

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

                webView.CoreWebView2.NavigationCompleted += OnNav;
                try
                {
                    Report($"Cargando {url} …");
                    webView.CoreWebView2.Navigate(url);

                    using var reg = cancellationToken.Register(() => navTcs.TrySetCanceled(cancellationToken));
                    await navTcs.Task.ConfigureAwait(true);
                }
                finally
                {
                    webView.CoreWebView2.NavigationCompleted -= OnNav;
                }

                Report("Esperando render JS / age-gate (8 s). Complete el challenge en la ventana si aparece…");
                await Task.Delay(8000, cancellationToken).ConfigureAwait(true);

                // Reintento corto si el DOM sigue vacío o es página de bloqueo.
                for (var attempt = 0; attempt < 3; attempt++)
                {
                    var html = await ReadDocumentHtmlAsync(webView).ConfigureAwait(true);
                    if (html.Length > 2_000 && !LooksLikeBlockPage(html))
                    {
                        Report($"HTML obtenido ({html.Length:N0} chars).");
                        return html;
                    }

                    Report(
                        attempt == 0
                            ? "HTML aún incompleto o bloqueado. Espere o complete el age-gate… (8 s más)"
                            : $"Reintento {attempt + 1}/3…");
                    await Task.Delay(8000, cancellationToken).ConfigureAwait(true);
                }

                var finalHtml = await ReadDocumentHtmlAsync(webView).ConfigureAwait(true);
                if (finalHtml.Length < 200)
                {
                    throw new InvalidOperationException(
                        "WebView2 no devolvió HTML útil. Complete el age-gate y vuelva a scrapear.");
                }

                Report($"HTML final ({finalHtml.Length:N0} chars).");
                return finalHtml;
            }
            finally
            {
                try
                {
                    host.Close();
                }
                catch
                {
                    // ignore
                }
            }
        }).Task.Unwrap();
    }

    private static async Task<string> ReadDocumentHtmlAsync(WebView2 webView)
    {
        var raw = await webView.CoreWebView2
            .ExecuteScriptAsync("document.documentElement ? document.documentElement.outerHTML : ''")
            .ConfigureAwait(true);

        return DecodeScriptString(raw);
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
            || sample.Contains("Attention Required", StringComparison.OrdinalIgnoreCase);
    }
}
