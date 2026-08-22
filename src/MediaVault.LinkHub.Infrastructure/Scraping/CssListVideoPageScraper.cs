using System.Globalization;
using System.Net;
using System.Text;

using AngleSharp;
using AngleSharp.Dom;

using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;

namespace MediaVault.LinkHub.Infrastructure.Scraping;

/// <summary>
/// Scraper genérico basado en selectores CSS descritos en <see cref="VideoScrapeHints"/>.
/// </summary>
public sealed class CssListVideoPageScraper : IVideoPageScraper
{
    public const string ScraperKey = "css-list";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IBrowserHtmlFetcher? _browserHtmlFetcher;

    public CssListVideoPageScraper(
        IHttpClientFactory httpClientFactory,
        IBrowserHtmlFetcher? browserHtmlFetcher = null)
    {
        _httpClientFactory = httpClientFactory;
        _browserHtmlFetcher = browserHtmlFetcher;
    }

    public string Key => ScraperKey;

    public async Task<VideoPageScrapeOutcome> ScrapeAsync(
        string startUrl,
        VideoScrapeHints hints,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startUrl);
        ArgumentNullException.ThrowIfNull(hints);

        var log = new List<string>();

        if (string.IsNullOrWhiteSpace(hints.ListItemSelector)
            && string.IsNullOrWhiteSpace(hints.TitleSelector))
        {
            throw new InvalidOperationException(
                "Los hints requieren ListItemSelector o TitleSelector.");
        }

        log.Add($"[{UtcNow()}] Inicio scrape · scraper={ScraperKey}");
        log.Add($"URL: {startUrl}");
        log.Add(DescribeHints(hints));

        var maxPages = Math.Clamp(hints.MaxPages <= 0 ? 1 : hints.MaxPages, 1, 50);
        var results = new List<ScrapedVideoCandidate>();
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var currentUrl = startUrl;
        var browsingContext = BrowsingContext.New(Configuration.Default);
        var pagesFetched = 0;
        var skippedNoUrl = 0;
        var skippedDuplicate = 0;

        for (var page = 0; page < maxPages && !string.IsNullOrWhiteSpace(currentUrl); page++)
        {
            if (hints.WaitMs is > 0)
            {
                log.Add($"Página {page + 1}: WaitMs={hints.WaitMs} ms…");
                await Task.Delay(hints.WaitMs.Value, cancellationToken).ConfigureAwait(false);
            }

            log.Add($"── Página {page + 1}/{maxPages} ──");
            log.Add($"GET {currentUrl}");

            FetchResult fetch;
            try
            {
                fetch = await FetchHtmlAsync(currentUrl, hints, log, cancellationToken).ConfigureAwait(false);
            }
            catch (VideoScrapeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Add($"ERROR HTTP: {ex.GetType().Name}: {ex.Message}");
                throw new VideoScrapeException(ex.Message, log.ToList(), ex);
            }

            pagesFetched++;
            log.Add(
                $"HTTP {(int)fetch.StatusCode} {fetch.StatusCode} · Content-Type={fetch.ContentType ?? "(ninguno)"} · HTML={fetch.Html.Length:N0} chars");

            if (fetch.Html.Length == 0)
            {
                log.Add("AVISO: cuerpo HTML vacío.");
                break;
            }

            var document = await browsingContext
                .OpenAsync(req => req.Content(fetch.Html).Address(currentUrl), cancellationToken)
                .ConfigureAwait(false);

            var docTitle = CleanText(document.Title) ?? "(sin <title>)";
            log.Add($"Documento: <title>={Truncate(docTitle, 80)}");
            LogSpaHints(log, fetch.Html, document);

            var pageBase = ResolveBaseUri(currentUrl, hints.BaseUrl, document);
            log.Add($"Base URL de resolución: {pageBase}");

            var parse = ParseItemsWithDiagnostics(document, hints, pageBase, log, page + 1);

            foreach (var item in parse.Items)
            {
                if (string.IsNullOrWhiteSpace(item.SourceUrl))
                {
                    skippedNoUrl++;
                    continue;
                }

                if (!seenUrls.Add(item.SourceUrl))
                {
                    skippedDuplicate++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Title))
                    item.Title = item.Code ?? item.SourceUrl;

                results.Add(item);
            }

            log.Add(
                $"Página {page + 1}: nodos={parse.NodeCount}, candidatos crudos={parse.Items.Count}, " +
                $"aceptados acumulados={results.Count}");

            // NextPageSelector es opcional: si no hay botón "siguiente", se conservan
            // los videos de las páginas ya parseadas y se termina sin error.
            if (page >= maxPages - 1)
            {
                log.Add($"Paginación: alcanzado MaxPages={maxPages}. Se conservan {results.Count} video(s).");
                break;
            }

            if (string.IsNullOrWhiteSpace(hints.NextPageSelector))
            {
                log.Add("Paginación: NextPageSelector no configurado · solo página actual (OK).");
                break;
            }

            var nextEl = document.QuerySelector(hints.NextPageSelector);
            if (nextEl is null)
            {
                log.Add(
                    $"Paginación: «{hints.NextPageSelector}» no está en esta página " +
                    "(normal si aún no hay más resultados). Se conservan los videos ya extraídos · OK.");
                break;
            }

            var nextHref = nextEl.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(nextHref))
            {
                log.Add(
                    "Paginación: el nodo «siguiente» existe pero sin href · " +
                    "se conservan los videos ya extraídos · OK.");
                break;
            }

            var nextUrl = ResolveUrl(pageBase, nextHref);
            if (string.Equals(nextUrl, currentUrl, StringComparison.OrdinalIgnoreCase))
            {
                log.Add("Paginación: href de «siguiente» apunta a la misma URL · OK, fin.");
                break;
            }

            log.Add($"Paginación: botón siguiente encontrado → {nextUrl}");
            currentUrl = nextUrl;
        }

        if (skippedNoUrl > 0)
            log.Add($"Omitidos sin URL: {skippedNoUrl}");
        if (skippedDuplicate > 0)
            log.Add($"Omitidos duplicados: {skippedDuplicate}");

        log.Add($"[{UtcNow()}] Fin · páginas={pagesFetched} · videos únicos={results.Count}");

        if (results.Count == 0)
        {
            log.Add(
                "DIAGNÓSTICO: 0 videos. Causas típicas: (1) ListItemSelector no coincide con el HTML, " +
                "(2) la página llega vacía/age-gate/login, (3) el listado se renderiza con JavaScript.");
        }

        return new VideoPageScrapeOutcome
        {
            Items = results,
            Log = log,
            PagesFetched = pagesFetched
        };
    }

    private async Task<FetchResult> FetchHtmlAsync(
        string url,
        VideoScrapeHints hints,
        List<string> log,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("VideoScraper");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyBrowserHeaders(request, url, hints);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var contentType = response.Content.Headers.ContentType?.ToString();

        if (response.IsSuccessStatusCode)
            return new FetchResult(response.StatusCode, contentType, html);

        log.Add(
            $"HTTP {(int)response.StatusCode} {response.StatusCode} (HttpClient). " +
            $"HTML={html.Length:N0} chars.");

        var blockedStatus = response.StatusCode is HttpStatusCode.Forbidden
            or HttpStatusCode.Unauthorized
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.ServiceUnavailable;

        var shouldUseBrowser =
            _browserHtmlFetcher is not null
            && (blockedStatus || LooksLikeBotBlock(html));

        if (!shouldUseBrowser)
        {
            throw new VideoScrapeException(
                $"La petición devolvió {(int)response.StatusCode} {response.StatusCode}. HTML={html.Length} chars. " +
                "El sitio bloquea clientes HTTP; configure Cookie en cabeceras extra o use un sitio compatible.",
                log.ToList());
        }

        log.Add(
            "El sitio bloquea HttpClient (403/anti-bot). Reintentando con WebView2 " +
            "(puede pedir age-gate en la ventana emergente)…");

        string browserHtml;
        try
        {
            browserHtml = await _browserHtmlFetcher!
                .FetchHtmlAsync(url, progress: null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Add($"ERROR WebView2: {ex.GetType().Name}: {ex.Message}");
            throw new VideoScrapeException(
                $"HTTP {(int)response.StatusCode} y WebView2 falló: {ex.Message}",
                log.ToList(),
                ex);
        }

        if (string.IsNullOrWhiteSpace(browserHtml))
        {
            throw new VideoScrapeException(
                $"HTTP {(int)response.StatusCode} y WebView2 no devolvió HTML. " +
                "Complete el age-gate y vuelva a scrapear.",
                log.ToList());
        }

        log.Add($"WebView2 OK · HTML={browserHtml.Length:N0} chars");
        return new FetchResult(HttpStatusCode.OK, "text/html; charset=utf-8", browserHtml);
    }

    private static void ApplyBrowserHeaders(HttpRequestMessage request, string url, VideoScrapeHints hints)
    {
        var userAgent = string.IsNullOrWhiteSpace(hints.UserAgent)
            ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
            : hints.UserAgent.Trim();

        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        request.Headers.TryAddWithoutValidation(
            "Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,es;q=0.8");
        request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
        request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
        request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
        request.Headers.TryAddWithoutValidation("Pragma", "no-cache");

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var origin = $"{uri.Scheme}://{uri.Host}/";
            request.Headers.TryAddWithoutValidation("Referer", origin);
            request.Headers.TryAddWithoutValidation("Origin", $"{uri.Scheme}://{uri.Host}");
        }

        if (hints.ExtraHeaders is null)
            return;

        foreach (var (headerName, headerValue) in hints.ExtraHeaders)
        {
            if (string.IsNullOrWhiteSpace(headerName) || headerValue is null)
                continue;

            request.Headers.Remove(headerName);
            if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
                request.Content?.Headers.TryAddWithoutValidation(headerName, headerValue);
        }
    }

    private static bool LooksLikeBotBlock(string html)
    {
        if (string.IsNullOrEmpty(html) || html.Length > 50_000)
            return false;

        return html.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Just a moment", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Access denied", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Attention Required", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Enable JavaScript and cookies", StringComparison.OrdinalIgnoreCase);
    }

    private static ParseResult ParseItemsWithDiagnostics(
        IDocument document,
        VideoScrapeHints hints,
        Uri pageBase,
        List<string> log,
        int pageNumber)
    {
        List<IElement> nodes;
        if (string.IsNullOrWhiteSpace(hints.ListItemSelector))
        {
            nodes = document.QuerySelectorAll(hints.TitleSelector!).Cast<IElement>().ToList();
            log.Add(
                $"Selector principal: TitleSelector «{hints.TitleSelector}» → {nodes.Count} nodo(s) " +
                "(sin ListItemSelector)");
        }
        else
        {
            nodes = document.QuerySelectorAll(hints.ListItemSelector).ToList();
            log.Add($"ListItemSelector «{hints.ListItemSelector}» → {nodes.Count} nodo(s)");
        }

        if (nodes.Count == 0)
        {
            LogSelectorMissHints(log, document, hints);
            return new ParseResult([], 0);
        }

        var fieldHits = new FieldHitCounters();
        var items = new List<ScrapedVideoCandidate>(nodes.Count);
        var sampleLimit = Math.Min(3, nodes.Count);

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            IParentNode scope = string.IsNullOrWhiteSpace(hints.ListItemSelector)
                ? (IParentNode?)node.ParentElement ?? document
                : node;

            var titleEl = ResolveRelative(scope, node, hints.TitleSelector);
            var urlEl = ResolveRelative(scope, node, hints.UrlSelector) ?? titleEl ?? node;
            var thumbEl = ResolveRelative(scope, node, hints.ThumbnailSelector)
                ?? node.QuerySelector("img")
                ?? node.QuerySelector("picture source, source");
            var previewEl = ResolveRelative(scope, node, hints.PreviewSelector);
            var codeEl = ResolveRelative(scope, node, hints.CodeSelector);
            var dateEl = ResolveRelative(scope, node, hints.DateSelector);
            var durationEl = ResolveRelative(scope, node, hints.DurationSelector);

            if (titleEl is not null) fieldHits.Title++;
            if (!string.IsNullOrWhiteSpace(hints.UrlSelector) && ResolveRelative(scope, node, hints.UrlSelector) is not null)
                fieldHits.Url++;
            else if (urlEl is not null)
                fieldHits.UrlFallback++;

            if (thumbEl is not null) fieldHits.Thumbnail++;
            if (codeEl is not null) fieldHits.Code++;
            if (dateEl is not null) fieldHits.Date++;
            if (durationEl is not null) fieldHits.Duration++;

            var href = urlEl?.GetAttribute(hints.UrlAttribute) ?? urlEl?.GetAttribute("href");
            if (string.IsNullOrWhiteSpace(href) && urlEl is not null && urlEl.LocalName == "a")
                href = urlEl.GetAttribute("href");

            var sourceUrl = string.IsNullOrWhiteSpace(href) ? string.Empty : ResolveUrl(pageBase, href);
            var title = CleanText(titleEl?.TextContent)
                ?? CleanText(urlEl?.GetAttribute("title"))
                ?? CleanText(thumbEl?.GetAttribute("title"))
                ?? CleanText(urlEl?.TextContent)
                ?? string.Empty;
            var thumbAttr = ResolveThumbnailAttribute(thumbEl, hints.ThumbnailAttribute)
                ?? ExtractBackgroundImageUrl(thumbEl)
                ?? ExtractBackgroundImageUrl(node);
            var thumbnailUrl = string.IsNullOrWhiteSpace(thumbAttr)
                ? null
                : ResolveUrl(pageBase, thumbAttr);

            var previewRaw = ResolvePreviewAttribute(previewEl, node, hints.PreviewAttribute);
            var previewUrl = string.IsNullOrWhiteSpace(previewRaw)
                ? null
                : ResolveUrl(pageBase, previewRaw);
            if (previewUrl is not null)
                fieldHits.Preview++;

            var dateText = CleanText(dateEl?.TextContent);
            var publishedAt = TryParseDate(dateText, hints.DateFormat);
            if (dateEl is not null && publishedAt is null && !string.IsNullOrWhiteSpace(dateText))
                fieldHits.DateParseFail++;

            var candidate = new ScrapedVideoCandidate
            {
                Title = title,
                SourceUrl = sourceUrl,
                ThumbnailUrl = thumbnailUrl,
                PreviewUrl = previewUrl,
                Code = CleanText(codeEl?.TextContent),
                DurationText = CleanText(durationEl?.TextContent),
                PublishedAt = publishedAt
            };
            items.Add(candidate);

            if (i < sampleLimit)
            {
                log.Add(
                    $"  Muestra[{i + 1}]: title={(string.IsNullOrWhiteSpace(title) ? "∅" : Truncate(title, 60))} · " +
                    $"url={(string.IsNullOrWhiteSpace(sourceUrl) ? "∅" : Truncate(sourceUrl, 80))} · " +
                    $"code={(candidate.Code is null ? "∅" : candidate.Code)} · " +
                    $"thumb={(thumbnailUrl is null ? "∅" : Truncate(thumbnailUrl, 70))} · " +
                    $"preview={(previewUrl is null ? "∅" : Truncate(previewUrl, 70))} · " +
                    $"dur={(candidate.DurationText is null ? "∅" : candidate.DurationText)} · " +
                    $"date={(publishedAt?.ToString("yyyy-MM-dd") ?? (dateText is null ? "∅" : $"no-parse «{Truncate(dateText, 30)}»"))}");
            }
        }

        log.Add(
            $"Página {pageNumber} · campos con hit (sobre {nodes.Count} nodos): " +
            $"title={fieldHits.Title}, url={fieldHits.Url}" +
            (fieldHits.UrlFallback > 0 ? $"+fallback={fieldHits.UrlFallback}" : string.Empty) +
            $", thumb={DescribeOptional(hints.ThumbnailSelector, fieldHits.Thumbnail)}, " +
            $"preview={fieldHits.Preview}, " +
            $"code={DescribeOptional(hints.CodeSelector, fieldHits.Code)}, " +
            $"date={DescribeOptional(hints.DateSelector, fieldHits.Date)}, " +
            $"duration={DescribeOptional(hints.DurationSelector, fieldHits.Duration)}");

        if (fieldHits.Preview == 0)
        {
            log.Add(
                "INFO: ningún PreviewUrl en el HTML del listado. En sitios como Brazzers el trailer al hover " +
                "suele inyectarse con JavaScript (no está en el GET). Configure PreviewSelector/PreviewAttribute " +
                "si el sitio lo expone, o use zoom de miniatura al pasar el cursor.");
        }

        if (fieldHits.DateParseFail > 0)
        {
            log.Add(
                $"AVISO: {fieldHits.DateParseFail} fecha(s) no parseables" +
                (string.IsNullOrWhiteSpace(hints.DateFormat)
                    ? " (configure DateFormat si el formato es fijo)."
                    : $" con DateFormat «{hints.DateFormat}»."));
        }

        var withoutUrl = items.Count(i => string.IsNullOrWhiteSpace(i.SourceUrl));
        if (withoutUrl > 0)
            log.Add($"AVISO: {withoutUrl}/{items.Count} candidato(s) sin URL (UrlSelector/UrlAttribute).");

        return new ParseResult(items, nodes.Count);
    }

    private static void LogSelectorMissHints(List<string> log, IDocument document, VideoScrapeHints hints)
    {
        log.Add("AVISO: 0 nodos con el selector principal. El HTML no contiene esos elementos.");

        if (!string.IsNullOrWhiteSpace(hints.TitleSelector)
            && !string.IsNullOrWhiteSpace(hints.ListItemSelector))
        {
            var titleHits = document.QuerySelectorAll(hints.TitleSelector).Length;
            log.Add($"  Prueba TitleSelector «{hints.TitleSelector}» a nivel documento → {titleHits} nodo(s)");
        }

        if (!string.IsNullOrWhiteSpace(hints.UrlSelector))
        {
            var urlHits = document.QuerySelectorAll(hints.UrlSelector).Length;
            log.Add($"  Prueba UrlSelector «{hints.UrlSelector}» a nivel documento → {urlHits} nodo(s)");
        }

        var anchors = document.QuerySelectorAll("a[href]").Length;
        var images = document.QuerySelectorAll("img").Length;
        var scripts = document.QuerySelectorAll("script").Length;
        log.Add($"  Conteo bruto del documento: a[href]={anchors}, img={images}, script={scripts}");
    }

    private static void LogSpaHints(List<string> log, string html, IDocument document)
    {
        var scriptCount = document.QuerySelectorAll("script").Length;
        var bodyTextLen = CleanText(document.Body?.TextContent)?.Length ?? 0;

        if (html.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
            || html.Contains("challenge-platform", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Attention Required", StringComparison.OrdinalIgnoreCase))
        {
            log.Add("AVISO: el HTML parece un challenge/Cloudflare. El scraper HTTP no puede pasar ese muro.");
        }

        if (html.Contains("age", StringComparison.OrdinalIgnoreCase)
            && (html.Contains("gate", StringComparison.OrdinalIgnoreCase)
                || html.Contains("verify", StringComparison.OrdinalIgnoreCase)
                || html.Contains("18+", StringComparison.OrdinalIgnoreCase)))
        {
            log.Add("AVISO: posible age-gate / verificación de edad en el HTML.");
        }

        if (scriptCount >= 8 && bodyTextLen < 400)
        {
            log.Add(
                $"AVISO: posible página SPA (scripts={scriptCount}, texto visible≈{bodyTextLen}). " +
                "El listado puede no estar en el HTML del GET inicial.");
        }
    }

    private static string DescribeHints(VideoScrapeHints hints)
    {
        var sb = new StringBuilder("Hints: ");
        sb.Append("listItem=").Append(Quote(hints.ListItemSelector));
        sb.Append(", title=").Append(Quote(hints.TitleSelector));
        sb.Append(", url=").Append(Quote(hints.UrlSelector));
        sb.Append(" [attr=").Append(hints.UrlAttribute).Append(']');
        sb.Append(", thumb=").Append(Quote(hints.ThumbnailSelector));
        sb.Append(" [attr=").Append(hints.ThumbnailAttribute).Append(']');
        sb.Append(", preview=").Append(Quote(hints.PreviewSelector));
        if (!string.IsNullOrWhiteSpace(hints.PreviewAttribute))
            sb.Append(" [attr=").Append(hints.PreviewAttribute).Append(']');
        sb.Append(", code=").Append(Quote(hints.CodeSelector));
        sb.Append(", date=").Append(Quote(hints.DateSelector));
        if (!string.IsNullOrWhiteSpace(hints.DateFormat))
            sb.Append(" [fmt=").Append(hints.DateFormat).Append(']');
        sb.Append(", duration=").Append(Quote(hints.DurationSelector));
        sb.Append(", next=").Append(Quote(hints.NextPageSelector));
        sb.Append(", maxPages=").Append(hints.MaxPages);
        if (!string.IsNullOrWhiteSpace(hints.BaseUrl))
            sb.Append(", baseUrl=").Append(hints.BaseUrl);
        return sb.ToString();
    }

    private static string DescribeOptional(string? selector, int hits) =>
        string.IsNullOrWhiteSpace(selector) ? "n/c" : hits.ToString();

    private static string Quote(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "∅" : $"«{value}»";

    private static string? ResolvePreviewAttribute(
        IElement? previewEl,
        IElement listItem,
        string? preferredAttribute)
    {
        if (previewEl is not null)
        {
            if (!string.IsNullOrWhiteSpace(preferredAttribute))
            {
                var preferred = ExtractImageUrlCandidate(previewEl.GetAttribute(preferredAttribute));
                if (!string.IsNullOrWhiteSpace(preferred))
                    return preferred;
            }

            foreach (var attributeName in PreviewAttributeCandidates)
            {
                var value = ExtractImageUrlCandidate(previewEl.GetAttribute(attributeName));
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        // Barrido del ítem y descendientes: atributos típicos de hover-preview.
        foreach (var element in EnumerateSelfAndDescendants(listItem))
        {
            foreach (var attributeName in PreviewAttributeCandidates)
            {
                var value = ExtractImageUrlCandidate(element.GetAttribute(attributeName));
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                if (LooksLikePreviewMedia(value))
                    return value;
            }
        }

        return null;
    }

    private static IEnumerable<IElement> EnumerateSelfAndDescendants(IElement root)
    {
        yield return root;
        foreach (var child in root.QuerySelectorAll("*"))
            yield return child;
    }

    private static readonly string[] PreviewAttributeCandidates =
    [
        "data-preview",
        "data-preview-url",
        "data-trailer",
        "data-trailer-url",
        "data-mp4",
        "data-video",
        "data-src-preview",
        "data-hover",
        "data-src",
        "href",
        "src"
    ];

    private static bool LooksLikePreviewMedia(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains(".mp4", StringComparison.Ordinal)
            || lower.Contains(".webm", StringComparison.Ordinal)
            || lower.Contains(".gif", StringComparison.Ordinal)
            || lower.Contains("trailer", StringComparison.Ordinal)
            || lower.Contains("preview", StringComparison.Ordinal);
    }

    private static string? ResolveThumbnailAttribute(IElement? thumbEl, string preferredAttribute)
    {
        if (thumbEl is null)
            return null;

        string? firstNonEmpty = null;
        foreach (var attributeName in EnumerateThumbnailAttributes(preferredAttribute))
        {
            var value = thumbEl.GetAttribute(attributeName);
            var resolved = ExtractImageUrlCandidate(value);
            if (string.IsNullOrWhiteSpace(resolved))
                continue;

            firstNonEmpty ??= resolved;
            if (!LooksLikePlaceholderImageUrl(resolved))
                return resolved;
        }

        return firstNonEmpty;
    }

    private static IEnumerable<string> EnumerateThumbnailAttributes(string preferredAttribute)
    {
        yield return preferredAttribute;
        if (!preferredAttribute.Equals("src", StringComparison.OrdinalIgnoreCase))
            yield return "src";
        yield return "data-src";
        yield return "data-lazy-src";
        yield return "data-original";
        yield return "data-thumb";
        yield return "data-thumbnail";
        yield return "data-image";
        yield return "data-bg";
        yield return "data-background";
        yield return "srcset";
        yield return "data-srcset";
    }

    /// <summary>
    /// Acepta URL directa o el primer/mejor candidato de un srcset (`url 1x, url2 2x`).
    /// Ignora placeholders data: y vacíos.
    /// </summary>
    private static string? ExtractImageUrlCandidate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        // srcset: "https://…/a.jpg 1x, https://…/a@2x.jpg 2x" — preferir el último (suele ser mayor res)
        if (trimmed.Contains(',', StringComparison.Ordinal) || trimmed.Contains(' ', StringComparison.Ordinal))
        {
            var parts = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                var last = parts[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (last.Length > 0)
                    trimmed = last[0];
            }
        }

        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static bool LooksLikePlaceholderImageUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains("placeholder", StringComparison.Ordinal)
            || lower.Contains("blank.", StringComparison.Ordinal)
            || lower.Contains("spacer", StringComparison.Ordinal)
            || lower.Contains("lazy-load", StringComparison.Ordinal)
            || lower.Contains("data:image", StringComparison.Ordinal)
            || lower.EndsWith(".gif", StringComparison.Ordinal) && lower.Contains("1x1", StringComparison.Ordinal);
    }

    private static string? ExtractBackgroundImageUrl(IElement? element)
    {
        if (element is null)
            return null;

        var style = element.GetAttribute("style");
        if (string.IsNullOrWhiteSpace(style))
            return null;

        // background-image: url("…") / url('…') / url(…)
        const string marker = "url(";
        var idx = style.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var start = idx + marker.Length;
        var end = style.IndexOf(')', start);
        if (end <= start)
            return null;

        var raw = style[start..end].Trim().Trim('"', '\'');
        return ExtractImageUrlCandidate(raw);
    }

    private static IElement? ResolveRelative(IParentNode scope, IElement item, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            return null;

        if (scope is IElement scopeElement)
            return scopeElement.QuerySelector(selector) ?? item.QuerySelector(selector);

        return item.QuerySelector(selector);
    }

    private static Uri ResolveBaseUri(string currentUrl, string? hintsBaseUrl, IDocument document)
    {
        if (!string.IsNullOrWhiteSpace(hintsBaseUrl)
            && Uri.TryCreate(hintsBaseUrl, UriKind.Absolute, out var configured))
            return configured;

        if (document.BaseUri is not null && Uri.TryCreate(document.BaseUri, UriKind.Absolute, out var docBase))
            return docBase;

        return new Uri(currentUrl, UriKind.Absolute);
    }

    private static string ResolveUrl(Uri pageBase, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
            return absolute.AbsoluteUri;

        return new Uri(pageBase, href).AbsoluteUri;
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return string.Join(
            ' ',
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static DateTime? TryParseDate(string? text, string? format)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!string.IsNullOrWhiteSpace(format)
            && DateTime.TryParseExact(
                text,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
            return exact;

        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
            return parsed;

        return null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";

    private static string UtcNow() => DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    private sealed record FetchResult(HttpStatusCode StatusCode, string? ContentType, string Html);

    private sealed record ParseResult(List<ScrapedVideoCandidate> Items, int NodeCount);

    private sealed class FieldHitCounters
    {
        public int Title;
        public int Url;
        public int UrlFallback;
        public int Thumbnail;
        public int Preview;
        public int Code;
        public int Date;
        public int Duration;
        public int DateParseFail;
    }
}
