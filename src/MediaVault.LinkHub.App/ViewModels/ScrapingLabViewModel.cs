using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ScrapingLabViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IVideoScrapeProbeService _scrapeProbeService;
    private readonly IHoverPreviewCaptureService _hoverPreviewCaptureService;
    private readonly IWebLinkService _webLinkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private int _thumbnailGeneration;
    private int _previewGeneration;

    public ScrapingLabViewModel(
        IVideoScrapeProbeService scrapeProbeService,
        IHoverPreviewCaptureService hoverPreviewCaptureService,
        IWebLinkService webLinkService,
        IHttpClientFactory httpClientFactory)
    {
        _scrapeProbeService = scrapeProbeService;
        _hoverPreviewCaptureService = hoverPreviewCaptureService;
        _webLinkService = webLinkService;
        _httpClientFactory = httpClientFactory;
        Hints.LoadTemplate();
    }

    public string Title => "Scraping Lab";

    public string Subtitle =>
        "Dry-run de selectores CSS y verificación de carga de previews (sin persistir)";

    public ScrapeHintsFormViewModel Hints { get; } = new();

    public ObservableCollection<WebLink> WebLinks { get; } = [];

    public ObservableCollection<ScrapingLabCandidateItem> Candidates { get; } = [];

    [ObservableProperty]
    private string _sampleUrl = string.Empty;

    [ObservableProperty]
    private WebLink? _selectedWebLink;

    [ObservableProperty]
    private bool _loadProducerHints;

    [ObservableProperty]
    private string _scraperKey = "css-list";

    [ObservableProperty]
    private ScrapingLabCandidateItem? _selectedCandidate;

    [ObservableProperty]
    private string _diagnosticLog =
        "Configure URL + selectores y pulse «Probar selectores».";

    [ObservableProperty]
    private string _statusText = "Listo.";

    [ObservableProperty]
    private string? _previewMediaUrl;

    [ObservableProperty]
    private bool _previewIsVideo;

    [ObservableProperty]
    private bool _previewIsImage;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private ScrapingLabPreviewState _previewState = ScrapingLabPreviewState.None;

    [ObservableProperty]
    private string _previewStatusText = "Seleccione un candidato con PreviewUrl.";

    /// <summary>
    /// Incrementado para pedir al code-behind que recargue el MediaElement.
    /// </summary>
    [ObservableProperty]
    private int _previewPlayToken;

    public string PreviewRefererUrl =>
        !string.IsNullOrWhiteSpace(SampleUrl)
            ? SampleUrl.Trim()
            : SelectedWebLink?.Url ?? string.Empty;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(LoadWebLinksAsync, "Cargando sitios...");

    private async Task LoadWebLinksAsync()
    {
        var links = await _webLinkService.GetAllAsync().ConfigureAwait(true);
        WebLinks.Clear();
        foreach (var link in links.OrderBy(l => l.Nombre, StringComparer.OrdinalIgnoreCase))
            WebLinks.Add(link);
    }

    partial void OnSelectedWebLinkChanged(WebLink? value)
    {
        if (value is null)
            return;

        ApplyHintsFromWebLink(value);

        if (string.IsNullOrWhiteSpace(SampleUrl))
            SampleUrl = value.Url;
    }

    partial void OnLoadProducerHintsChanged(bool value)
    {
        if (SelectedWebLink is not null)
            ApplyHintsFromWebLink(SelectedWebLink);
    }

    partial void OnSelectedCandidateChanged(ScrapingLabCandidateItem? value)
    {
        PreparePreviewForCandidate(value, autoPlay: true);
        TestSelectedPreviewCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ApplyHintsTemplate()
    {
        Hints.LoadTemplate();
        ScraperKey = "css-list";
        StatusText = "Plantilla CSS de listado aplicada.";
    }

    [RelayCommand]
    private void ApplySelectedWebLinkHints()
    {
        if (SelectedWebLink is null)
        {
            ErrorMessage = "Seleccione un sitio del Link Manager.";
            return;
        }

        ApplyHintsFromWebLink(SelectedWebLink);
        StatusText = LoadProducerHints
            ? $"Hints de productora cargados desde «{SelectedWebLink.Nombre}»."
            : $"Hints de actriz cargados desde «{SelectedWebLink.Nombre}».";
    }

    [RelayCommand]
    private async Task ResolveSampleUrlAsync()
    {
        if (SelectedWebLink is null)
        {
            ErrorMessage = "Seleccione un sitio para resolver una URL de muestra.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var url = await _scrapeProbeService
                .ResolveSampleUrlAsync(SelectedWebLink.Id, preferActressProfile: !LoadProducerHints)
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(url))
            {
                SampleUrl = SelectedWebLink.Url;
                StatusText = "Sin ActressLink/ProducerLink; se usó la URL del sitio.";
                return;
            }

            SampleUrl = url;
            StatusText = "URL de muestra resuelta desde enlaces guardados.";
        }, "Resolviendo URL de muestra...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ProbeSelectorsAsync()
    {
        if (!AppFeatureFlags.IsWebScrapingEnabled)
        {
            ErrorMessage = "El web scraping está desactivado temporalmente.";
            return;
        }

        if (!HasConfiguredHints())
        {
            ErrorMessage = "Configure al menos ListItemSelector o TitleSelector.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SampleUrl))
        {
            ErrorMessage = "Indique una URL de muestra.";
            return;
        }

        if (!Uri.TryCreate(SampleUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = "La URL de muestra no es un http(s) válido.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            Candidates.Clear();
            SelectedCandidate = null;
            ResetPreviewPanel();
            DiagnosticLog = "Ejecutando probe (máx. 1 página)…";
            StatusText = "Probando selectores…";

            var outcome = await _scrapeProbeService
                .ProbeAsync(SampleUrl.Trim(), Hints.ToModel(), ScraperKey)
                .ConfigureAwait(true);

            DiagnosticLog = outcome.Log.Count == 0
                ? "(Sin líneas de diagnóstico.)"
                : string.Join(Environment.NewLine, outcome.Log);

            var generation = ++_thumbnailGeneration;
            foreach (var candidate in outcome.Items)
            {
                var item = new ScrapingLabCandidateItem { Candidate = candidate };
                Candidates.Add(item);
                _ = LoadThumbnailAsync(item, generation);
            }

            StatusText = Candidates.Count == 0
                ? "0 candidatos — revise el log y ForceBrowser / PreferBrowserFetch."
                : $"{Candidates.Count} candidato(s) · {outcome.PagesFetched} página(s). Dry-run (no persistido).";
        }, "Probe en curso...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CaptureHoverPreviewsAsync()
    {
        if (string.IsNullOrWhiteSpace(Hints.ListItemSelector))
        {
            ErrorMessage = "Hace falta ListItemSelector para capturar previews por hover.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SampleUrl))
        {
            ErrorMessage = "Indique la URL del listado.";
            return;
        }

        if (Candidates.Count == 0)
        {
            ErrorMessage = "Ejecute primero «Probar selectores».";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var hints = Hints.ToModel();
            var log = new List<string>
            {
                $"[{DateTime.UtcNow:HH:mm:ss}] Captura hover (WebView2) — lab, sin persistir",
                $"URL: {SampleUrl.Trim()}",
                $"ListItemSelector: {hints.ListItemSelector}",
                $"PreviewHoverSelector: {hints.PreviewHoverSelector ?? "(auto)"}",
                $"PreviewHoverWaitMs: {(hints.PreviewHoverWaitMs <= 0 ? 900 : hints.PreviewHoverWaitMs)}"
            };

            var progress = new Progress<string>(message =>
            {
                StatusText = message;
                log.Add(message);
                DiagnosticLog = string.Join(Environment.NewLine, log);
            });

            var map = await _hoverPreviewCaptureService
                .CaptureAsync(
                    SampleUrl.Trim(),
                    hints.ListItemSelector!,
                    hints.PreviewHoverSelector,
                    hints.PreviewHoverWaitMs,
                    progress)
                .ConfigureAwait(true);

            var updated = 0;
            foreach (var item in Candidates)
            {
                var key = NormalizeUrlKey(item.SourceUrl);
                var match = map.FirstOrDefault(pair =>
                    string.Equals(NormalizeUrlKey(pair.Key), key, StringComparison.OrdinalIgnoreCase));

                if (string.IsNullOrWhiteSpace(match.Value))
                    continue;

                item.Candidate.PreviewUrl = match.Value;
                item.NotifyPreviewChanged();
                updated++;
            }

            log.Add($"Previews detectados: {map.Count}; aplicados a candidatos: {updated}");
            DiagnosticLog = string.Join(Environment.NewLine, log);
            StatusText = updated == 0
                ? "Sin previews aplicables (¿blob:? ¿age-gate?)."
                : $"{updated} preview(s) aplicados en memoria. Seleccione un ítem para verificar carga.";

            if (SelectedCandidate is not null)
                PreparePreviewForCandidate(SelectedCandidate, autoPlay: true);
        }, "Capturando previews…").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanTestSelectedPreview))]
    private void TestSelectedPreview()
    {
        if (SelectedCandidate is null)
            return;

        PreparePreviewForCandidate(SelectedCandidate, autoPlay: true);
    }

    private bool CanTestSelectedPreview() => SelectedCandidate is not null;

    [RelayCommand]
    private void CopyDiagnosticLog()
    {
        if (string.IsNullOrWhiteSpace(DiagnosticLog))
            return;

        try
        {
            Clipboard.SetText(DiagnosticLog);
            StatusText = "Log de diagnóstico copiado.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>Invocado desde code-behind cuando MediaElement abre el stream.</summary>
    public void NotifyPreviewOpened(int generation)
    {
        if (generation != _previewGeneration)
            return;

        PreviewState = ScrapingLabPreviewState.Ok;
        PreviewStatusText = "OK — el preview de video carga correctamente.";
    }

    /// <summary>Invocado desde code-behind cuando MediaElement falla.</summary>
    public void NotifyPreviewFailed(int generation, string? reason = null)
    {
        if (generation != _previewGeneration)
            return;

        PreviewState = ScrapingLabPreviewState.Error;
        PreviewStatusText = string.IsNullOrWhiteSpace(reason)
            ? "Error — el MediaElement no pudo cargar el preview (CDN / hotlink / formato)."
            : $"Error — {reason}";
        PreviewIsVideo = false;
    }

    public int CurrentPreviewGeneration => _previewGeneration;

    private void ApplyHintsFromWebLink(WebLink link)
    {
        if (LoadProducerHints)
        {
            Hints.LoadFromJson(link.ProducerScrapeHintsJson);
            ScraperKey = string.IsNullOrWhiteSpace(link.ProducerScraperKey)
                ? "css-list"
                : link.ProducerScraperKey!;
            return;
        }

        Hints.LoadFromJson(link.ScrapeHintsJson);
        ScraperKey = string.IsNullOrWhiteSpace(link.ScraperKey) ? "css-list" : link.ScraperKey!;
    }

    private void PreparePreviewForCandidate(ScrapingLabCandidateItem? item, bool autoPlay)
    {
        var generation = ++_previewGeneration;
        PreviewImage = item?.Thumbnail;

        if (item is null || !item.HasPreviewUrl)
        {
            PreviewMediaUrl = null;
            PreviewIsVideo = false;
            PreviewIsImage = false;
            PreviewState = ScrapingLabPreviewState.None;
            PreviewStatusText = item is null
                ? "Seleccione un candidato con PreviewUrl."
                : "Sin PreviewUrl en este candidato.";
            return;
        }

        if (item.PreviewIsBlob)
        {
            PreviewMediaUrl = null;
            PreviewIsVideo = false;
            PreviewIsImage = false;
            PreviewState = ScrapingLabPreviewState.BlobUnsupported;
            PreviewStatusText =
                "blob: solo vive en el WebView; no es reproducible en MediaElement de la app.";
            return;
        }

        if (item.PreviewIsVideo)
        {
            PreviewMediaUrl = item.PreviewUrl;
            PreviewIsVideo = true;
            PreviewIsImage = false;
            PreviewState = ScrapingLabPreviewState.Loading;
            PreviewStatusText = "Cargando preview de video…";
            if (autoPlay)
                PreviewPlayToken = generation;
            return;
        }

        PreviewMediaUrl = null;
        PreviewIsVideo = false;
        PreviewIsImage = true;
        PreviewState = ScrapingLabPreviewState.ImageOnly;
        PreviewStatusText = "Preview de imagen/animación (no es stream de video).";
        _ = LoadPreviewImageAsync(item, generation);
    }

    private void ResetPreviewPanel()
    {
        _previewGeneration++;
        PreviewMediaUrl = null;
        PreviewIsVideo = false;
        PreviewIsImage = false;
        PreviewImage = null;
        PreviewState = ScrapingLabPreviewState.None;
        PreviewStatusText = "Seleccione un candidato con PreviewUrl.";
    }

    private bool HasConfiguredHints() =>
        !string.IsNullOrWhiteSpace(Hints.ListItemSelector)
        || !string.IsNullOrWhiteSpace(Hints.TitleSelector);

    private async Task LoadThumbnailAsync(ScrapingLabCandidateItem item, int generation)
    {
        if (string.IsNullOrWhiteSpace(item.ThumbnailUrl))
            return;

        if (!Uri.TryCreate(item.ThumbnailUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("VideoScraper");
            var referer = string.IsNullOrWhiteSpace(PreviewRefererUrl)
                ? uri
                : new Uri(PreviewRefererUrl);
            var image = await RemoteHttpImageLoader
                .TryLoadAsync(client, uri, referer, decodePixelWidth: 160)
                .ConfigureAwait(true);

            if (image is null || generation != _thumbnailGeneration)
                return;

            item.Thumbnail = image;
            if (ReferenceEquals(SelectedCandidate, item) && PreviewImage is null)
                PreviewImage = image;
        }
        catch
        {
            // Miniatura opcional.
        }
    }

    private async Task LoadPreviewImageAsync(ScrapingLabCandidateItem item, int generation)
    {
        if (string.IsNullOrWhiteSpace(item.PreviewUrl))
            return;

        if (!Uri.TryCreate(item.PreviewUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("VideoScraper");
            var referer = string.IsNullOrWhiteSpace(PreviewRefererUrl)
                ? uri
                : new Uri(PreviewRefererUrl);
            var image = await RemoteHttpImageLoader
                .TryLoadAsync(client, uri, referer, decodePixelWidth: 480)
                .ConfigureAwait(true);

            if (image is null || generation != _previewGeneration)
                return;

            PreviewImage = image;
        }
        catch
        {
            if (generation == _previewGeneration)
            {
                PreviewState = ScrapingLabPreviewState.Error;
                PreviewStatusText = "Error al cargar la imagen de preview.";
            }
        }
    }

    private static string NormalizeUrlKey(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url.Trim();

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
    }
}
