using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

using MediaVault.LinkHub.Application.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MediaVault.LinkHub.App.Scraping;

/// <summary>
/// Captura genérica de previews: carga el listado en WebView2, simula hover y lee el media resultante.
/// </summary>
public sealed class WebView2HoverPreviewCaptureService : IHoverPreviewCaptureService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<IReadOnlyDictionary<string, string>> CaptureAsync(
        string listUrl,
        string listItemSelector,
        string? hoverSelector,
        int waitMs,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(listItemSelector);

        waitMs = Math.Clamp(waitMs <= 0 ? 900 : waitMs, 200, 10_000);

        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("No hay Dispatcher de WPF disponible.");

        return dispatcher.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var host = new Window
            {
                Title = "Captura de previews (navegador) — complete age-gate/login si aparece",
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
                        "Falta el runtime de WebView2 (Evergreen). Instálelo desde Microsoft y reintente.",
                        ex);
                }
                catch (Exception ex) when (ex.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "No se pudo inicializar WebView2. Verifique que el runtime esté instalado.",
                        ex);
                }

                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = true;

                var navigationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void OnNav(object? sender, CoreWebView2NavigationCompletedEventArgs args)
                {
                    if (args.IsSuccess)
                        navigationTcs.TrySetResult(true);
                    else
                        navigationTcs.TrySetException(
                            new InvalidOperationException($"Navegación falló (WebErrorStatus={args.WebErrorStatus})."));
                }

                webView.CoreWebView2.NavigationCompleted += OnNav;
                try
                {
                    Report($"Navegando a {listUrl}…");
                    webView.CoreWebView2.Navigate(listUrl);

                    using (cancellationToken.Register(() => navigationTcs.TrySetCanceled(cancellationToken)))
                        await navigationTcs.Task.ConfigureAwait(true);
                }
                finally
                {
                    webView.CoreWebView2.NavigationCompleted -= OnNav;
                }

                Report("Esperando hidratación del listado (3 s). Si hay age-gate, acéptelo en esta ventana…");
                await Task.Delay(3000, cancellationToken).ConfigureAwait(true);

                // Reintento suave: esperar a que existan ítems del selector.
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var countJson = await webView.CoreWebView2
                        .ExecuteScriptAsync($"document.querySelectorAll({ToJsString(listItemSelector)}).length")
                        .ConfigureAwait(true);
                    if (int.TryParse(countJson?.Trim('"'), out var count) && count > 0)
                    {
                        Report($"Listado listo: {count} ítem(s). Simulando hover…");
                        break;
                    }

                    if (attempt == 19)
                    {
                        throw new InvalidOperationException(
                            $"No se encontraron ítems con ListItemSelector «{listItemSelector}». " +
                            "Revise el selector o complete el age-gate/login en la ventana del navegador.");
                    }

                    Report($"Esperando ítems del listado… ({attempt + 1}/20)");
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(true);
                }

                var script = BuildCaptureScript(listItemSelector, hoverSelector, waitMs);
                Report("Capturando previews (puede tardar unos segundos por ítem)…");
                var raw = await webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
                var json = DecodeScriptJsonString(raw);
                var rows = JsonSerializer.Deserialize<List<CaptureRow>>(json, JsonOptions) ?? [];

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.Href) || string.IsNullOrWhiteSpace(row.Preview))
                        continue;

                    var preview = row.Preview.Trim();
                    if (preview.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
                    {
                        // blob: vive solo dentro del WebView; no es reproducible en MediaElement de la app.
                        continue;
                    }

                    map[row.Href.Trim()] = preview;
                }

                Report($"Captura terminada: {map.Count} preview(s) de {rows.Count} ítem(s).");
                await Task.Delay(600, cancellationToken).ConfigureAwait(true);
                return (IReadOnlyDictionary<string, string>)map;
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

    private static string BuildCaptureScript(string listItemSelector, string? hoverSelector, int waitMs)
    {
        var listJs = ToJsString(listItemSelector);
        var hoverJs = string.IsNullOrWhiteSpace(hoverSelector) ? "null" : ToJsString(hoverSelector);

        // Script async IIFE → Promise; ExecuteScriptAsync espera el resultado serializado.
        return $$"""
((async () => {
  const listItemSelector = {{listJs}};
  const hoverSelector = {{hoverJs}};
  const waitMs = {{waitMs}};
  const sleep = (ms) => new Promise(r => setTimeout(r, ms));

  const fire = (el, type) => {
    try {
      el.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, view: window, clientX: 5, clientY: 5 }));
    } catch (_) {}
  };

  const pickMedia = (root) => {
    if (!root) return null;
    const videos = root.querySelectorAll ? root.querySelectorAll('video') : [];
    for (const v of videos) {
      const src = v.currentSrc || v.src || (v.querySelector('source') && v.querySelector('source').src);
      if (src && !src.startsWith('blob:')) return src;
      if (src) return src;
    }
    const sources = root.querySelectorAll ? root.querySelectorAll('source[src]') : [];
    for (const s of sources) {
      const src = s.getAttribute('src');
      if (src) return src;
    }
    const imgs = root.querySelectorAll ? root.querySelectorAll('img[src]') : [];
    for (const img of imgs) {
      const src = img.currentSrc || img.src || '';
      if (/\.(gif|webp)(\?|$)/i.test(src) || /preview|trailer|hover/i.test(src)) return src;
    }
    return null;
  };

  const items = Array.from(document.querySelectorAll(listItemSelector));
  const results = [];

  for (const item of items) {
    const link = item.querySelector('a[href*="/video/"]')
      || item.querySelector('a[href*="/scene/"]')
      || item.querySelector('a[href]');
    const href = link ? (link.href || link.getAttribute('href')) : null;

    const hoverTarget = (hoverSelector && item.querySelector(hoverSelector))
      || item.querySelector('img')
      || item.querySelector('picture')
      || item.querySelector('a')
      || item;

    const before = pickMedia(item);
    fire(hoverTarget, 'pointerenter');
    fire(hoverTarget, 'mouseenter');
    fire(hoverTarget, 'mouseover');
    fire(hoverTarget, 'mousemove');

    let preview = null;
    const deadline = Date.now() + waitMs;
    while (Date.now() < deadline) {
      preview = pickMedia(item) || pickMedia(document.body);
      // Prefer media that appeared/changed after hover.
      if (preview && preview !== before) break;
      if (preview && (preview.includes('.mp4') || preview.includes('.webm') || preview.startsWith('blob:'))) break;
      await sleep(120);
    }

    if ((!preview || preview === before) && pickMedia(item))
      preview = pickMedia(item);

    fire(hoverTarget, 'mouseout');
    fire(hoverTarget, 'mouseleave');
    fire(hoverTarget, 'pointerleave');
    await sleep(80);

    results.push({ href, preview: preview || null });
  }

  return results;
})())
""";
    }

    private static string ToJsString(string value) =>
        JsonSerializer.Serialize(value);

    private static string DecodeScriptJsonString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "null")
            return "[]";

        // ExecuteScriptAsync returns a JSON-encoded string value.
        return JsonSerializer.Deserialize<string>(raw) ?? "[]";
    }

    private sealed class CaptureRow
    {
        public string? Href { get; set; }

        public string? Preview { get; set; }
    }
}
