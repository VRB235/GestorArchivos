using System.Collections.ObjectModel;
using System.Net.Http;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Ventana de enlaces de catálogo de una productora: tiles con logo + grid de videos scrapeados.
/// </summary>
public partial class ProducerLinksViewModel : ViewModelBase
{
    private readonly IProducerLinkService _producerLinkService;
    private readonly IVideoScrapeService _videoScrapeService;
    private readonly IWebLinkService _webLinkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppDialogService _appDialogService;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly Producer _producer;
    private int? _activeLinkIdForSeen;
    private int _thumbGeneration;

    public ProducerLinksViewModel(
        Producer producer,
        IProducerLinkService producerLinkService,
        IVideoScrapeService videoScrapeService,
        IWebLinkService webLinkService,
        IHttpClientFactory httpClientFactory,
        IAppDialogService appDialogService,
        IBrowserLauncher browserLauncher)
    {
        _producer = producer;
        _producerLinkService = producerLinkService;
        _videoScrapeService = videoScrapeService;
        _webLinkService = webLinkService;
        _httpClientFactory = httpClientFactory;
        _appDialogService = appDialogService;
        _browserLauncher = browserLauncher;

        LinkActions = new ObservableCollection<ActressLinkAction>(
            AppFeatureFlags.IsWebScrapingEnabled
                ? Enum.GetValues<ActressLinkAction>()
                : [ActressLinkAction.Browse]);
        LinkAction = AppFeatureFlags.IsWebScrapingEnabled
            ? ActressLinkAction.Scrape
            : ActressLinkAction.Browse;
        WindowTitle = $"Catálogo · {_producer.Name}";
        RefreshSiteScrapeSummary();
    }

    public string WindowTitle { get; }

    public string ProducerName => _producer.Name;

    public ObservableCollection<ProducerLinkTileItem> LinkTiles { get; } = [];

    public ObservableCollection<WebLink> AvailableWebLinks { get; } = [];

    public ObservableCollection<ScrapedVideoTileItem> ScrapedVideos { get; } = [];

    public ObservableCollection<ActressLinkAction> LinkActions { get; }

    [ObservableProperty]
    private ProducerLinkTileItem? _selectedLinkTile;

    public ProducerLink? SelectedLink => SelectedLinkTile?.Link;

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
    private string _siteScrapeSummary = string.Empty;

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

    public bool ShowSiteScrapeInfo =>
        AppFeatureFlags.IsWebScrapingEnabled && LinkAction == ActressLinkAction.Scrape;

    public bool HasSelectedScrapeLink =>
        AppFeatureFlags.IsWebScrapingEnabled
        && SelectedLink is not null
        && SelectedLink.Action == ActressLinkAction.Scrape;

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

    partial void OnSelectedLinkTileChanged(ProducerLinkTileItem? value)
    {
        OnPropertyChanged(nameof(SelectedLink));
        _ = OnSelectedLinkChangedAsync(value?.Link);
    }

    private async Task OnSelectedLinkChangedAsync(ProducerLink? value)
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
        SelectedWebLink = ResolveWebLink(value) ?? AvailableWebLinks.FirstOrDefault(w => w.Id == value.WebLinkId);
        RefreshSiteScrapeSummary();

        NotifyLinkState();

        if (value.Action == ActressLinkAction.Browse || !AppFeatureFlags.IsWebScrapingEnabled)
        {
            ScrapedVideos.Clear();
            VideosSummary = !AppFeatureFlags.IsWebScrapingEnabled
                ? "Web scraping desactivado temporalmente."
                : "Enlace de navegación: use «Abrir en Firefox».";
            return;
        }

        await LoadScrapedVideosAsync(value.Id).ConfigureAwait(true);
        _activeLinkIdForSeen = value.Id;
    }

    partial void OnLinkActionChanged(ActressLinkAction value)
    {
        OnPropertyChanged(nameof(ShowSiteScrapeInfo));
        RefreshSiteScrapeSummary();
    }

    partial void OnSelectedWebLinkChanged(WebLink? value)
    {
        if (value is not null && string.IsNullOrWhiteSpace(LinkTitle))
        {
            var nombre = value.Nombre.Trim();
            LinkTitle = nombre.Length <= 80 ? nombre : TruncateTitle(nombre, 80);
        }

        if (value is not null && string.IsNullOrWhiteSpace(LinkUrl))
            LinkUrl = value.Url;

        RefreshSiteScrapeSummary();
    }

    partial void OnLinkUrlChanged(string value)
    {
        if (SelectedWebLink is not null || string.IsNullOrWhiteSpace(value))
            return;

        var resolved = ResolveWebLink(new ProducerLink { Title = LinkTitle, Url = value });
        if (resolved is not null)
            SelectedWebLink = resolved;
    }

    private void ClearEditor()
    {
        LinkTitle = string.Empty;
        LinkUrl = string.Empty;
        LinkAction = AppFeatureFlags.IsWebScrapingEnabled
            ? ActressLinkAction.Scrape
            : ActressLinkAction.Browse;
        LinkNotes = string.Empty;
        SelectedWebLink = null;
        RefreshSiteScrapeSummary();
    }

    private void NotifyLinkState()
    {
        OnPropertyChanged(nameof(CanEditLink));
        OnPropertyChanged(nameof(SelectedLink));
        OnPropertyChanged(nameof(ShowSiteScrapeInfo));
        OnPropertyChanged(nameof(HasSelectedScrapeLink));
        SaveLinkCommand.NotifyCanExecuteChanged();
        DeleteLinkCommand.NotifyCanExecuteChanged();
        ScrapeSelectedLinkCommand.NotifyCanExecuteChanged();
        OpenSelectedInBrowserCommand.NotifyCanExecuteChanged();
    }

    private void RefreshSiteScrapeSummary()
    {
        if (LinkAction != ActressLinkAction.Scrape)
        {
            SiteScrapeSummary = string.Empty;
            return;
        }

        if (SelectedWebLink is null)
        {
            SiteScrapeSummary =
                "Seleccione el sitio del Link Manager. Los selectores de catálogo de productora se configuran ahí (Scraping productora).";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedWebLink.ProducerScrapeHintsJson))
        {
            SiteScrapeSummary =
                $"«{SelectedWebLink.Nombre}» aún no tiene selectores de scraping de productora. Ábralos en Link Manager → Scraping productora y guarde antes de scrapear.";
            return;
        }

        var key = string.IsNullOrWhiteSpace(SelectedWebLink.ProducerScraperKey)
            ? "css-list"
            : SelectedWebLink.ProducerScraperKey;
        var paginationHint = "Primera corrida: solo página 1. Luego pagina mientras haya videos nuevos.";
        SiteScrapeSummary =
            $"Usará los selectores de productora de «{SelectedWebLink.Nombre}» (ScraperKey={key}). {paginationHint}";
    }

    private async Task ReloadLinksAsync()
    {
        var previousId = SelectedLinkTile?.Link.Id;
        LinkTiles.Clear();

        var links = await _producerLinkService.GetByProducerIdAsync(_producer.Id).ConfigureAwait(true);
        foreach (var link in links)
        {
            var resolved = ResolveWebLink(link);
            if (resolved is not null && link.WebLinkId is null)
            {
                try
                {
                    await _producerLinkService.UpdateAsync(
                        link.Id,
                        resolved.Id,
                        link.Title,
                        link.Url,
                        link.Action,
                        link.Notes).ConfigureAwait(true);
                    link.WebLinkId = resolved.Id;
                    link.WebLink = resolved;
                }
                catch
                {
                    // Logo se muestra igual; la persistencia puede reintentarse al guardar.
                }
            }

            LinkTiles.Add(new ProducerLinkTileItem
            {
                Link = link,
                ResolvedWebLink = resolved
            });
        }

        SelectedLinkTile = previousId is int id
            ? LinkTiles.FirstOrDefault(tile => tile.Link.Id == id)
            : LinkTiles.FirstOrDefault();
    }

    private WebLink? ResolveWebLink(ProducerLink link)
    {
        if (link.WebLinkId is int webLinkId)
        {
            var byId = AvailableWebLinks.FirstOrDefault(w => w.Id == webLinkId)
                ?? link.WebLink;
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

    private async Task LoadScrapedVideosAsync(int producerLinkId)
    {
        ScrapedVideos.Clear();
        var generation = ++_thumbGeneration;

        var videos = await _videoScrapeService
            .GetPersistedByProducerLinkAsync(producerLinkId)
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
            var referer = SelectedLink?.Url;
            if (string.IsNullOrWhiteSpace(referer)
                || !Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                refererUri = Uri.TryCreate(tile.SourceUrl, UriKind.Absolute, out var sourceUri)
                    ? sourceUri
                    : new Uri($"{uri.Scheme}://{uri.Host}/");
            }

            var client = _httpClientFactory.CreateClient("VideoScraper");
            var image = await RemoteHttpImageLoader
                .TryLoadAsync(client, uri, refererUri, decodePixelWidth: 220)
                .ConfigureAwait(true);

            if (image is null || generation != _thumbGeneration)
                return;

            tile.Thumbnail = image;
        }
        catch
        {
            // Miniatura remota opcional.
        }
    }

    private async Task MarkActiveLinkSeenAsync()
    {
        if (_activeLinkIdForSeen is not int linkId)
            return;

        try
        {
            await _videoScrapeService.MarkProducerVideosSeenAsync(linkId).ConfigureAwait(true);
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
    private async Task AddLinkAsync()
    {
        IsEditorExpanded = true;

        if (string.IsNullOrWhiteSpace(LinkUrl))
        {
            ErrorMessage = "Indique la URL del listado de videos de la productora.";
            return;
        }

        if (SelectedWebLink is null)
        {
            SelectedWebLink = ResolveWebLink(new ProducerLink
            {
                Title = LinkTitle,
                Url = LinkUrl
            });
        }

        if (string.IsNullOrWhiteSpace(LinkTitle))
        {
            LinkTitle = SelectedWebLink?.Nombre is { Length: > 0 } nombre
                ? TruncateTitle(nombre.Trim(), 80)
                : "Catálogo";
        }

        if (LinkAction == ActressLinkAction.Scrape && SelectedWebLink is null)
        {
            ErrorMessage =
                "Seleccione el sitio del Link Manager. Sin ese sitio no se pueden usar los selectores de scraping.";
            return;
        }

        if (LinkAction == ActressLinkAction.Scrape
            && SelectedWebLink is not null
            && string.IsNullOrWhiteSpace(SelectedWebLink.ProducerScrapeHintsJson))
        {
            ErrorMessage =
                $"El sitio «{SelectedWebLink.Nombre}» no tiene selectores de scraping de productora. " +
                "Configurelos en Link Manager → Scraping productora y guarde el sitio.";
            return;
        }

        try
        {
            ErrorMessage = null;
            var created = await _producerLinkService.CreateAsync(
                _producer.Id,
                SelectedWebLink?.Id,
                LinkTitle,
                LinkUrl,
                LinkAction,
                LinkNotes).ConfigureAwait(true);

            await ReloadLinksAsync().ConfigureAwait(true);
            SelectedLinkTile = LinkTiles.FirstOrDefault(tile => tile.Link.Id == created.Id);
            IsEditorExpanded = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            IsEditorExpanded = true;
        }
    }

    private static string TruncateTitle(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 1)].TrimEnd() + "…";

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

        if (LinkAction == ActressLinkAction.Scrape && SelectedWebLink is null)
        {
            ErrorMessage = "Seleccione el sitio del Link Manager para scrapear.";
            return;
        }

        try
        {
            ErrorMessage = null;
            await _producerLinkService.UpdateAsync(
                SelectedLink.Id,
                SelectedWebLink?.Id
                    ?? ResolveWebLink(new ProducerLink { Title = LinkTitle, Url = LinkUrl, WebLinkId = SelectedLink.WebLinkId })?.Id,
                LinkTitle,
                LinkUrl,
                LinkAction,
                LinkNotes).ConfigureAwait(true);

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

            await _producerLinkService.DeleteAsync(id).ConfigureAwait(true);
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
            var opened = await _producerLinkService.OpenInBrowserAsync(SelectedLink.Id).ConfigureAwait(true);
            if (!opened)
                ErrorMessage = "No se pudo abrir el enlace en Firefox.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedScrapeLink))]
    private async Task ScrapeSelectedLinkAsync()
    {
        if (!AppFeatureFlags.IsWebScrapingEnabled)
        {
            ErrorMessage = "El web scraping está desactivado temporalmente.";
            return;
        }

        if (SelectedLink is null || SelectedLink.Action != ActressLinkAction.Scrape)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var site = SelectedWebLink ?? SelectedLink.WebLink;
            var hintsJson = site?.ProducerScrapeHintsJson;
            var scraperKey = site?.ProducerScraperKey ?? "css-list";

            var header = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scraping «{SelectedLink.Title}»",
                $"ProducerId={_producer.Id} · ProducerLinkId={SelectedLink.Id} · Action={SelectedLink.Action}",
                $"WebLinkId={site?.Id.ToString() ?? "(null)"} · Sitio={site?.Nombre ?? "(sin sitio)"}",
                $"ProducerScraperKey={scraperKey}",
                $"URL catálogo: {SelectedLink.Url}",
                $"LastScrapedAt={(SelectedLink.LastScrapedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "(primera corrida → MaxPages=1)")}",
                "—— Hints de productora (JSON) ——",
                string.IsNullOrWhiteSpace(hintsJson)
                    ? "(sin ProducerScrapeHintsJson — configurelos en Link Manager → Scraping productora)"
                    : hintsJson.Trim(),
                "—— Ejecución ——"
            };

            try
            {
                var result = await _videoScrapeService
                    .ScrapeAndPersistByProducerLinkAsync(SelectedLink.Id)
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

        if (!_browserLauncher.TryOpenInFirefox(item.SourceUrl))
            ErrorMessage = "No se pudo abrir el video en Firefox.";
    }
}
