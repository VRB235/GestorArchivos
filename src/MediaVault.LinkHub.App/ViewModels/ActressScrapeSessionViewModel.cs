using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ActressScrapeSessionViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions HintsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IVideoScrapeService _videoScrapeService;
    private readonly IHoverPreviewCaptureService _hoverPreviewCaptureService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ActressLink _link;
    private readonly string _actressName;
    private int _thumbnailGeneration;

    public ActressScrapeSessionViewModel(
        IVideoScrapeService videoScrapeService,
        IHoverPreviewCaptureService hoverPreviewCaptureService,
        IHttpClientFactory httpClientFactory,
        ActressLink link,
        string actressName)
    {
        _videoScrapeService = videoScrapeService;
        _hoverPreviewCaptureService = hoverPreviewCaptureService;
        _httpClientFactory = httpClientFactory;
        _link = link;
        _actressName = actressName;
    }

    public string WindowTitle => $"Scraping · {_actressName}";

    public string SourceSummary => $"{_link.Title} — {_link.Url}";

    public ObservableCollection<ScrapedVideoListItem> Results { get; } = [];

    public ObservableCollection<string> Warnings { get; } = [];

    [ObservableProperty]
    private ScrapedVideoListItem? _selectedResult;

    [ObservableProperty]
    private string _statusText = "Listo para scrapear.";

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    private string _diagnosticLog = "Ejecute el scraping para ver el diagnóstico (HTTP, selectores, coincidencias).";

    [ObservableProperty]
    private bool _showHoverPreview;

    [ObservableProperty]
    private bool _hoverPreviewIsVideo;

    [ObservableProperty]
    private string? _hoverPreviewMediaUrl;

    [ObservableProperty]
    private ImageSource? _hoverPreviewImage;

    [ObservableProperty]
    private string _hoverPreviewCaption = string.Empty;

    public string RefererUrl => _link.Url;

    public void BeginHoverPreview(ScrapedVideoListItem item)
    {
        HoverPreviewCaption = item.Title;
        HoverPreviewIsVideo = item.PreviewIsVideo;
        HoverPreviewMediaUrl = item.PreviewIsVideo ? item.PreviewUrl : null;
        HoverPreviewImage = item.Thumbnail;
        ShowHoverPreview = item.Thumbnail is not null || item.PreviewIsVideo;
    }

    public void EndHoverPreview()
    {
        ShowHoverPreview = false;
        HoverPreviewIsVideo = false;
        HoverPreviewMediaUrl = null;
        HoverPreviewImage = null;
        HoverPreviewCaption = string.Empty;
    }

    public async Task InitializeAsync()
    {
        var existing = await _videoScrapeService
            .GetPersistedByLinkAsync(_link.Id)
            .ConfigureAwait(true);

        await ReplaceResultsAsync(existing).ConfigureAwait(true);

        if (existing.Count > 0)
        {
            HasRun = true;
            StatusText = $"{existing.Count} video(s) persistidos del último scrape.";
        }
    }

    [RelayCommand]
    private async Task RunScrapeAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            Warnings.Clear();
            DiagnosticLog = "Descargando y parseando…";
            StatusText = "Descargando y parseando…";

            var result = await _videoScrapeService
                .ScrapeAndPersistAsync(_link.Id)
                .ConfigureAwait(true);

            var persisted = await _videoScrapeService
                .GetPersistedByLinkAsync(_link.Id)
                .ConfigureAwait(true);

            await ReplaceResultsAsync(persisted).ConfigureAwait(true);

            foreach (var warning in result.Warnings)
                Warnings.Add(warning);

            DiagnosticLog = result.DiagnosticLog.Count == 0
                ? "(Sin líneas de diagnóstico.)"
                : string.Join(Environment.NewLine, result.DiagnosticLog);

            HasRun = true;
            StatusText = Results.Count == 0
                ? "Sin resultados — revise el log de diagnóstico."
                : $"{Results.Count} video(s) extraídos y guardados · {result.PagesFetched} página(s).";
        }, "Scraping en curso...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CaptureHoverPreviewsAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            var hints = DeserializeHintsOrThrow();
            if (string.IsNullOrWhiteSpace(hints.ListItemSelector))
                throw new InvalidOperationException(
                    "Para captura genérica de previews haga falta ListItemSelector en ScrapeHintsJson.");

            if (Results.Count == 0)
            {
                Warnings.Clear();
                Warnings.Add("No hay videos persistidos. Ejecute primero el scraping del listado.");
                return;
            }

            var log = new List<string>
            {
                $"[{DateTime.UtcNow:HH:mm:ss}] Captura de previews vía WebView2 (hover simulado)",
                $"URL: {_link.Url}",
                $"ListItemSelector: {hints.ListItemSelector}",
                $"PreviewHoverSelector: {hints.PreviewHoverSelector ?? "(auto: img/picture/a)"}",
                $"PreviewHoverWaitMs: {(hints.PreviewHoverWaitMs <= 0 ? 900 : hints.PreviewHoverWaitMs)}"
            };

            var progress = new Progress<string>(message =>
            {
                StatusText = message;
                log.Add(message);
                DiagnosticLog = string.Join(Environment.NewLine, log);
            });

            StatusText = "Abriendo navegador embebido…";
            var map = await _hoverPreviewCaptureService
                .CaptureAsync(
                    _link.Url,
                    hints.ListItemSelector!,
                    hints.PreviewHoverSelector,
                    hints.PreviewHoverWaitMs,
                    progress)
                .ConfigureAwait(true);

            log.Add($"Previews detectados: {map.Count}");
            foreach (var pair in map.Take(8))
                log.Add($"  {Truncate(pair.Key, 70)} → {Truncate(pair.Value, 70)}");

            var updated = await _videoScrapeService
                .UpdatePreviewUrlsAsync(_link.Id, map)
                .ConfigureAwait(true);

            log.Add($"Actualizados en BD: {updated}");
            DiagnosticLog = string.Join(Environment.NewLine, log);

            var persisted = await _videoScrapeService
                .GetPersistedByLinkAsync(_link.Id)
                .ConfigureAwait(true);
            await ReplaceResultsAsync(persisted).ConfigureAwait(true);

            StatusText = updated == 0
                ? "No se pudo capturar previews (¿age-gate? ¿hover no genera <video>?)."
                : $"{updated} preview(s) capturados. Pase el cursor sobre la miniatura.";
        }, "Capturando previews…").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCopyDiagnosticLog))]
    private void CopyDiagnosticLog()
    {
        if (string.IsNullOrWhiteSpace(DiagnosticLog))
            return;

        try
        {
            Clipboard.SetText(DiagnosticLog);
            StatusText = "Log de diagnóstico copiado al portapapeles.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedResult))]
    private void OpenSelectedResult()
    {
        if (SelectedResult is null || string.IsNullOrWhiteSpace(SelectedResult.SourceUrl))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = SelectedResult.SourceUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private VideoScrapeHints DeserializeHintsOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_link.ScrapeHintsJson))
            throw new InvalidOperationException("El enlace no tiene ScrapeHintsJson.");

        return JsonSerializer.Deserialize<VideoScrapeHints>(_link.ScrapeHintsJson, HintsJsonOptions)
            ?? throw new InvalidOperationException("ScrapeHintsJson inválido.");
    }

    private async Task ReplaceResultsAsync(IReadOnlyList<ScrapedVideo> videos)
    {
        Results.Clear();
        SelectedResult = null;

        var items = new List<ScrapedVideoListItem>(videos.Count);
        foreach (var video in videos)
        {
            var item = new ScrapedVideoListItem { Video = video };
            Results.Add(item);
            items.Add(item);
        }

        var generation = ++_thumbnailGeneration;
        foreach (var item in items)
            _ = LoadThumbnailAsync(item, generation);
    }

    private async Task LoadThumbnailAsync(ScrapedVideoListItem item, int generation)
    {
        if (string.IsNullOrWhiteSpace(item.ThumbnailUrl))
            return;

        if (!Uri.TryCreate(item.ThumbnailUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("VideoScraper");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Referrer = new Uri(_link.Url);
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/*,*/*;q=0.8");

            using var response = await client.SendAsync(request).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
                return;

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(true);
            if (bytes.Length == 0 || generation != _thumbnailGeneration)
                return;

            var image = await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.DecodePixelWidth = 160;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }).ConfigureAwait(true);

            if (generation != _thumbnailGeneration)
                return;

            item.Thumbnail = image;
        }
        catch
        {
            // Miniatura remota opcional; no bloquea la lista.
        }
    }

    partial void OnSelectedResultChanged(ScrapedVideoListItem? value) =>
        OpenSelectedResultCommand.NotifyCanExecuteChanged();

    partial void OnDiagnosticLogChanged(string value) =>
        CopyDiagnosticLogCommand.NotifyCanExecuteChanged();

    private bool HasSelectedResult() => SelectedResult is not null;

    private bool CanCopyDiagnosticLog() =>
        !string.IsNullOrWhiteSpace(DiagnosticLog)
        && !DiagnosticLog.StartsWith("Ejecute el scraping", StringComparison.Ordinal)
        && DiagnosticLog != "Descargando y parseando…";

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
