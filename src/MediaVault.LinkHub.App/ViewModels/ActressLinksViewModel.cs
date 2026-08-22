using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Ventana de enlaces de una actriz: tiles con logo de Link Manager + grid de videos scrapeados.
/// </summary>
public partial class ActressLinksViewModel : ViewModelBase
{
    private readonly IActressLinkService _actressLinkService;
    private readonly IVideoScrapeService _videoScrapeService;
    private readonly IWebLinkService _webLinkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppDialogService _appDialogService;
    private readonly Actress _actress;
    private int? _activeLinkIdForSeen;
    private int _thumbGeneration;

    public ActressLinksViewModel(
        Actress actress,
        IActressLinkService actressLinkService,
        IVideoScrapeService videoScrapeService,
        IWebLinkService webLinkService,
        IHttpClientFactory httpClientFactory,
        IAppDialogService appDialogService)
    {
        _actress = actress;
        _actressLinkService = actressLinkService;
        _videoScrapeService = videoScrapeService;
        _webLinkService = webLinkService;
        _httpClientFactory = httpClientFactory;
        _appDialogService = appDialogService;

        LinkActions = new ObservableCollection<ActressLinkAction>(Enum.GetValues<ActressLinkAction>());
        LinkAction = ActressLinkAction.Scrape;
        LinkHints.LoadTemplate();
        WindowTitle = $"Enlaces · {actress.Name}";
    }

    public string WindowTitle { get; }

    public string ActressName => _actress.Name;

    public ObservableCollection<ActressLinkTileItem> LinkTiles { get; } = [];

    public ObservableCollection<WebLink> AvailableWebLinks { get; } = [];

    public ObservableCollection<ScrapedVideoTileItem> ScrapedVideos { get; } = [];

    public ObservableCollection<ActressLinkAction> LinkActions { get; }

    public ScrapeHintsFormViewModel LinkHints { get; } = new();

    [ObservableProperty]
    private ActressLinkTileItem? _selectedLinkTile;

    public ActressLink? SelectedLink => SelectedLinkTile?.Link;

    [ObservableProperty]
    private WebLink? _selectedWebLink;

    [ObservableProperty]
    private string _linkTitle = string.Empty;

    [ObservableProperty]
    private string _linkUrl = string.Empty;

    [ObservableProperty]
    private ActressLinkAction _linkAction;

    [ObservableProperty]
    private string _linkNotes = string.Empty;

    [ObservableProperty]
    private string _linkScraperKey = "css-list";

    [ObservableProperty]
    private string _videosSummary = "Seleccione un enlace para ver sus videos.";

    [ObservableProperty]
    private bool _isEditorExpanded;

    [ObservableProperty]
    private bool _isScrapeLogExpanded;

    [ObservableProperty]
    private string _diagnosticLog =
        "El log de scraping aparecerá aquí al ejecutar «Scrapear / actualizar».";

    public bool CanEditLink => SelectedLink is not null;

    public bool ShowScrapeHints => LinkAction == ActressLinkAction.Scrape;

    public bool HasSelectedScrapeLink =>
        SelectedLink is not null && SelectedLink.Action == ActressLinkAction.Scrape;

    public async Task InitializeAsync()
    {
        await RunBusyCoreAsync(async () =>
        {
            var webLinks = await _webLinkService.GetAllAsync().ConfigureAwait(true);
            AvailableWebLinks.Clear();
            foreach (var link in webLinks.OrderBy(l => l.Nombre))
                AvailableWebLinks.Add(link);

            await ReloadLinksAsync().ConfigureAwait(true);
        }, "Cargando enlaces...").ConfigureAwait(true);
    }

    public async Task OnClosingAsync()
    {
        await MarkActiveLinkSeenAsync().ConfigureAwait(true);
    }

    partial void OnSelectedLinkTileChanged(ActressLinkTileItem? value)
    {
        OnPropertyChanged(nameof(SelectedLink));
        _ = OnSelectedLinkChangedAsync(value?.Link);
    }

    private async Task OnSelectedLinkChangedAsync(ActressLink? value)
    {
        await MarkActiveLinkSeenAsync().ConfigureAwait(true);

        if (value is null)
        {
            ClearEditor();
            ScrapedVideos.Clear();
            VideosSummary = "Seleccione un enlace para ver sus videos.";
            NotifyLinkState();
            return;
        }

        LinkTitle = value.Title;
        LinkUrl = value.Url;
        LinkAction = value.Action;
        LinkNotes = value.Notes ?? string.Empty;
        LinkScraperKey = value.ScraperKey ?? "css-list";
        LinkHints.LoadFromJson(value.ScrapeHintsJson);
        SelectedWebLink = ResolveWebLink(value) ?? AvailableWebLinks.FirstOrDefault(w => w.Id == value.WebLinkId);

        NotifyLinkState();

        if (value.Action == ActressLinkAction.Browse)
        {
            ScrapedVideos.Clear();
            VideosSummary = "Enlace de navegación: use «Abrir en navegador».";
            return;
        }

        await LoadScrapedVideosAsync(value.Id).ConfigureAwait(true);
        _activeLinkIdForSeen = value.Id;
    }

    partial void OnLinkActionChanged(ActressLinkAction value)
    {
        OnPropertyChanged(nameof(ShowScrapeHints));
    }

    partial void OnSelectedWebLinkChanged(WebLink? value)
    {
        if (value is null)
            return;

        if (string.IsNullOrWhiteSpace(LinkTitle))
            LinkTitle = value.Nombre;
    }

    private void ClearEditor()
    {
        LinkTitle = string.Empty;
        LinkUrl = string.Empty;
        LinkAction = ActressLinkAction.Scrape;
        LinkNotes = string.Empty;
        LinkScraperKey = "css-list";
        LinkHints.LoadTemplate();
        SelectedWebLink = null;
    }

    private void NotifyLinkState()
    {
        OnPropertyChanged(nameof(CanEditLink));
        OnPropertyChanged(nameof(SelectedLink));
        OnPropertyChanged(nameof(ShowScrapeHints));
        OnPropertyChanged(nameof(HasSelectedScrapeLink));
        SaveLinkCommand.NotifyCanExecuteChanged();
        DeleteLinkCommand.NotifyCanExecuteChanged();
        ScrapeSelectedLinkCommand.NotifyCanExecuteChanged();
        OpenSelectedInBrowserCommand.NotifyCanExecuteChanged();
    }

    private async Task ReloadLinksAsync()
    {
        var previousId = SelectedLinkTile?.Link.Id;
        LinkTiles.Clear();

        var links = await _actressLinkService.GetByActressIdAsync(_actress.Id).ConfigureAwait(true);
        foreach (var link in links)
        {
            var resolved = ResolveWebLink(link);
            if (resolved is not null && link.WebLinkId is null)
            {
                try
                {
                    await _actressLinkService.UpdateAsync(
                        link.Id,
                        resolved.Id,
                        link.Title,
                        link.Url,
                        link.Action,
                        link.Notes,
                        link.ScrapeHintsJson,
                        link.ScraperKey).ConfigureAwait(true);
                    link.WebLinkId = resolved.Id;
                    link.WebLink = resolved;
                }
                catch
                {
                    // Logo se muestra igual; la persistencia puede reintentarse al guardar.
                }
            }

            LinkTiles.Add(new ActressLinkTileItem
            {
                Link = link,
                ResolvedWebLink = resolved
            });
        }

        SelectedLinkTile = previousId is int id
            ? LinkTiles.FirstOrDefault(tile => tile.Link.Id == id)
            : LinkTiles.FirstOrDefault();
    }

    private WebLink? ResolveWebLink(ActressLink link)
    {
        if (link.WebLinkId is int webLinkId)
        {
            var byId = AvailableWebLinks.FirstOrDefault(w => w.Id == webLinkId)
                ?? link.WebLink;
            if (byId is not null && !string.IsNullOrWhiteSpace(byId.LogoPath))
                return byId;
            if (byId is not null)
                return byId;
        }

        var title = link.Title.Trim();
        if (title.Length > 0)
        {
            var byName = AvailableWebLinks.FirstOrDefault(w =>
                string.Equals(w.Nombre, title, StringComparison.OrdinalIgnoreCase));
            if (byName is not null)
                return byName;

            var byProducer = AvailableWebLinks.FirstOrDefault(w =>
                w.Producers.Any(p => string.Equals(p.Name, title, StringComparison.OrdinalIgnoreCase)));
            if (byProducer is not null)
                return byProducer;

            var byContains = AvailableWebLinks.FirstOrDefault(w =>
                w.Nombre.Contains(title, StringComparison.OrdinalIgnoreCase)
                || title.Contains(w.Nombre, StringComparison.OrdinalIgnoreCase));
            if (byContains is not null)
                return byContains;
        }

        if (Uri.TryCreate(link.Url, UriKind.Absolute, out var linkUri))
        {
            var host = linkUri.Host.TrimStart('.');
            return AvailableWebLinks.FirstOrDefault(w =>
                Uri.TryCreate(w.Url, UriKind.Absolute, out var webUri)
                && (string.Equals(webUri.Host, host, StringComparison.OrdinalIgnoreCase)
                    || webUri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith("." + webUri.Host, StringComparison.OrdinalIgnoreCase)));
        }

        return link.WebLink;
    }

    private async Task LoadScrapedVideosAsync(int actressLinkId)
    {
        ScrapedVideos.Clear();
        var generation = ++_thumbGeneration;

        var videos = await _videoScrapeService
            .GetPersistedByLinkAsync(actressLinkId)
            .ConfigureAwait(true);

        var newCount = videos.Count(v => v.IsNew);
        VideosSummary = videos.Count == 0
            ? "Sin videos scrapeados. Use «Scrapear / actualizar»."
            : $"{videos.Count} videos · {newCount} nuevos";

        foreach (var video in videos.OrderByDescending(v => v.IsNew).ThenByDescending(v => v.ScrapedAt))
        {
            var tile = new ScrapedVideoTileItem { Video = video };
            ScrapedVideos.Add(tile);
            _ = LoadRemoteThumbnailAsync(tile, generation);
        }
    }

    private async Task LoadRemoteThumbnailAsync(ScrapedVideoTileItem tile, int generation)
    {
        if (string.IsNullOrWhiteSpace(tile.ThumbnailUrl)
            || !Uri.TryCreate(tile.ThumbnailUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("VideoScraper");
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Accept", "image/avif,image/webp,image/apng,image/*,*/*;q=0.8");

            // Muchos CDN de estudios exigen Referer de la página listado/ficha.
            var referer = SelectedLink?.Url;
            if (string.IsNullOrWhiteSpace(referer)
                || !Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                refererUri = Uri.TryCreate(tile.SourceUrl, UriKind.Absolute, out var sourceUri)
                    ? sourceUri
                    : new Uri($"{uri.Scheme}://{uri.Host}/");
            }

            request.Headers.Referrer = refererUri;

            using var response = await client.SendAsync(request).ConfigureAwait(true);
            if (!response.IsSuccessStatusCode)
                return;

            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(true);
            if (bytes.Length == 0 || generation != _thumbGeneration)
                return;

            var image = await Task.Run(() =>
            {
                using var stream = new System.IO.MemoryStream(bytes);
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.DecodePixelWidth = 220;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }).ConfigureAwait(true);

            if (generation != _thumbGeneration)
                return;

            tile.Thumbnail = image;
        }
        catch
        {
            // Miniatura remota opcional: CDN/403/formato no soportado por WPF.
        }
    }

    private async Task MarkActiveLinkSeenAsync()
    {
        if (_activeLinkIdForSeen is not int linkId)
            return;

        try
        {
            await _videoScrapeService.MarkVideosSeenAsync(linkId).ConfigureAwait(true);
        }
        catch
        {
            // No bloquear cierre/cambio de enlace.
        }
        finally
        {
            _activeLinkIdForSeen = null;
        }
    }

    [RelayCommand]
    private void ToggleEditor() => IsEditorExpanded = !IsEditorExpanded;

    [RelayCommand]
    private void NewLink()
    {
        SelectedLinkTile = null;
        ClearEditor();
        IsEditorExpanded = true;
        NotifyLinkState();
    }

    [RelayCommand]
    private void ApplyHintsTemplate()
    {
        LinkHints.LoadTemplate();
        LinkScraperKey = "css-list";
        LinkAction = ActressLinkAction.Scrape;
    }

    [RelayCommand]
    private async Task AddLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(LinkTitle) || string.IsNullOrWhiteSpace(LinkUrl))
        {
            ErrorMessage = "Indique título y URL del enlace (URL específica de la actriz).";
            return;
        }

        try
        {
            ErrorMessage = null;
            var webLinkId = SelectedWebLink?.Id
                ?? ResolveWebLink(new ActressLink { Title = LinkTitle, Url = LinkUrl })?.Id;

            await _actressLinkService.CreateAsync(
                _actress.Id,
                webLinkId,
                LinkTitle,
                LinkUrl,
                LinkAction,
                LinkNotes,
                LinkAction == ActressLinkAction.Scrape ? LinkHints.ToJson() : null,
                LinkAction == ActressLinkAction.Scrape ? LinkScraperKey : null).ConfigureAwait(true);

            await ReloadLinksAsync().ConfigureAwait(true);
            IsEditorExpanded = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditLink))]
    private async Task SaveLinkAsync()
    {
        if (SelectedLink is null)
            return;

        if (string.IsNullOrWhiteSpace(LinkTitle) || string.IsNullOrWhiteSpace(LinkUrl))
        {
            ErrorMessage = "Indique título y URL del enlace.";
            return;
        }

        try
        {
            ErrorMessage = null;
            await _actressLinkService.UpdateAsync(
                SelectedLink.Id,
                SelectedWebLink?.Id
                    ?? ResolveWebLink(new ActressLink { Title = LinkTitle, Url = LinkUrl, WebLinkId = SelectedLink.WebLinkId })?.Id,
                LinkTitle,
                LinkUrl,
                LinkAction,
                LinkNotes,
                LinkAction == ActressLinkAction.Scrape ? LinkHints.ToJson() : null,
                LinkAction == ActressLinkAction.Scrape ? LinkScraperKey : null).ConfigureAwait(true);

            await ReloadLinksAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditLink))]
    private async Task DeleteLinkAsync()
    {
        if (SelectedLink is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar el enlace «{SelectedLink.Title}» y sus videos scrapeados?",
                AppDialogKind.Question))
            return;

        try
        {
            ErrorMessage = null;
            var id = SelectedLink.Id;
            if (_activeLinkIdForSeen == id)
                _activeLinkIdForSeen = null;

            await _actressLinkService.DeleteAsync(id).ConfigureAwait(true);
            await ReloadLinksAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditLink))]
    private async Task OpenSelectedInBrowserAsync()
    {
        if (SelectedLink is null)
            return;

        try
        {
            ErrorMessage = null;
            var opened = await _actressLinkService.OpenInBrowserAsync(SelectedLink.Id).ConfigureAwait(true);
            if (!opened)
                ErrorMessage = "No se pudo abrir el enlace en el navegador.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScrapeLink))]
    private async Task ScrapeSelectedLinkAsync()
    {
        if (SelectedLink is null || SelectedLink.Action != ActressLinkAction.Scrape)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var header = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scraping «{SelectedLink.Title}»",
                $"ActressId={_actress.Id} · ActressLinkId={SelectedLink.Id} · Action={SelectedLink.Action}",
                $"ScraperKey={SelectedLink.ScraperKey ?? "css-list"}",
                $"URL: {SelectedLink.Url}",
                "—— Hints (JSON) ——",
                string.IsNullOrWhiteSpace(SelectedLink.ScrapeHintsJson)
                    ? "(sin ScrapeHintsJson)"
                    : SelectedLink.ScrapeHintsJson!.Trim(),
                "—— Ejecución ——"
            };

            try
            {
                var result = await _videoScrapeService
                    .ScrapeAndPersistAsync(SelectedLink.Id)
                    .ConfigureAwait(true);

                var body = result.DiagnosticLog.Count == 0
                    ? ["(sin líneas de diagnóstico del scraper)"]
                    : result.DiagnosticLog;

                var footer = new List<string>
                {
                    "—— Resumen ——",
                    $"Páginas={result.PagesFetched} · candidatos={result.Items.Count} · avisos={result.Warnings.Count}",
                    $"Con ThumbnailUrl: {result.Items.Count(i => !string.IsNullOrWhiteSpace(i.ThumbnailUrl))}",
                    $"Sin ThumbnailUrl: {result.Items.Count(i => string.IsNullOrWhiteSpace(i.ThumbnailUrl))}"
                };

                if (result.Warnings.Count > 0)
                {
                    footer.Add("—— Avisos ——");
                    footer.AddRange(result.Warnings);
                    ErrorMessage = string.Join(" · ", result.Warnings.Take(3));
                    IsScrapeLogExpanded = true;
                }
                else
                {
                    ErrorMessage = null;
                }

                DiagnosticLog = string.Join(
                    Environment.NewLine,
                    header.Concat(body).Concat(footer));
            }
            catch (MediaVault.LinkHub.Application.Models.Scraping.VideoScrapeException ex)
            {
                var body = ex.DiagnosticLog.Count == 0
                    ? Array.Empty<string>()
                    : ex.DiagnosticLog;
                DiagnosticLog = string.Join(
                    Environment.NewLine,
                    header.Concat(body).Concat(
                    [
                        "—— ERROR ——",
                        $"{ex.GetType().Name}: {ex.Message}",
                        ex.InnerException is null ? "" : $"Inner: {ex.InnerException.Message}"
                    ]).Where(line => line.Length > 0));
                IsScrapeLogExpanded = true;
                throw;
            }
            catch (Exception ex)
            {
                DiagnosticLog = string.Join(
                    Environment.NewLine,
                    header.Concat(
                    [
                        "—— ERROR ——",
                        $"{ex.GetType().Name}: {ex.Message}",
                        ex.InnerException is null ? "" : $"Inner: {ex.InnerException.Message}",
                        ex.StackTrace ?? ""
                    ]).Where(line => line.Length > 0));
                IsScrapeLogExpanded = true;
                throw;
            }

            await LoadScrapedVideosAsync(SelectedLink.Id).ConfigureAwait(true);
            _activeLinkIdForSeen = SelectedLink.Id;
        }, "Scrapeando videos...").ConfigureAwait(true);
    }

    [RelayCommand]
    private void ToggleScrapeLog() => IsScrapeLogExpanded = !IsScrapeLogExpanded;

    [RelayCommand(CanExecute = nameof(CanCopyScrapeLog))]
    private void CopyScrapeLog()
    {
        if (string.IsNullOrWhiteSpace(DiagnosticLog))
            return;

        try
        {
            System.Windows.Clipboard.SetText(DiagnosticLog);
            AppendScrapeLog($"[{DateTime.Now:HH:mm:ss}] Log copiado al portapapeles.");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo copiar el log: {ex.Message}";
        }
    }

    partial void OnDiagnosticLogChanged(string value) =>
        CopyScrapeLogCommand.NotifyCanExecuteChanged();

    private bool CanCopyScrapeLog() =>
        !string.IsNullOrWhiteSpace(DiagnosticLog)
        && !DiagnosticLog.StartsWith("El log de scraping aparecerá", StringComparison.Ordinal);

    private void AppendScrapeLog(string line)
    {
        if (string.IsNullOrWhiteSpace(DiagnosticLog)
            || DiagnosticLog.StartsWith("El log de scraping aparecerá", StringComparison.Ordinal))
        {
            DiagnosticLog = line;
            return;
        }

        DiagnosticLog += Environment.NewLine + line;
    }

    [RelayCommand]
    private void OpenScrapedVideo(ScrapedVideoTileItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.SourceUrl))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(item.SourceUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
