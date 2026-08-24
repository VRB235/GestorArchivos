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
/// Ventana de enlaces de una actriz: tiles con logo de Link Manager + grid de videos scrapeados.
/// </summary>
public partial class ActressLinksViewModel : ViewModelBase
{
    private readonly IActressLinkService _actressLinkService;
    private readonly IVideoScrapeService _videoScrapeService;
    private readonly IWebLinkService _webLinkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAppDialogService _appDialogService;
    private readonly IBrowserLauncher _browserLauncher;
    private readonly Actress _actress;
    private int? _activeLinkIdForSeen;
    private int _thumbGeneration;

    public ActressLinksViewModel(
        Actress actress,
        IActressLinkService actressLinkService,
        IVideoScrapeService videoScrapeService,
        IWebLinkService webLinkService,
        IHttpClientFactory httpClientFactory,
        IAppDialogService appDialogService,
        IBrowserLauncher browserLauncher)
    {
        _actress = actress;
        _actressLinkService = actressLinkService;
        _videoScrapeService = videoScrapeService;
        _webLinkService = webLinkService;
        _httpClientFactory = httpClientFactory;
        _appDialogService = appDialogService;
        _browserLauncher = browserLauncher;

        LinkActions = new ObservableCollection<ActressLinkAction>(Enum.GetValues<ActressLinkAction>());
        LinkAction = ActressLinkAction.Scrape;
        WindowTitle = $"Enlaces · {actress.Name}";
        RefreshSiteScrapeSummary();
    }

    public string WindowTitle { get; }

    public string ActressName => _actress.Name;

    public ObservableCollection<ActressLinkTileItem> LinkTiles { get; } = [];

    public ObservableCollection<WebLink> AvailableWebLinks { get; } = [];

    public ObservableCollection<ScrapedVideoTileItem> ScrapedVideos { get; } = [];

    public ObservableCollection<ActressLinkAction> LinkActions { get; }

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

    public bool ShowSiteScrapeInfo => LinkAction == ActressLinkAction.Scrape;

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
        SelectedWebLink = ResolveWebLink(value) ?? AvailableWebLinks.FirstOrDefault(w => w.Id == value.WebLinkId);
        RefreshSiteScrapeSummary();

        NotifyLinkState();

        if (value.Action == ActressLinkAction.Browse)
        {
            ScrapedVideos.Clear();
            VideosSummary = "Enlace de navegación: use «Abrir en Firefox».";
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
            // Evitar títulos kilométricos si el WebLink se guardó con un título de video.
            var nombre = value.Nombre.Trim();
            LinkTitle = nombre.Length <= 80 ? nombre : TruncateTitle(nombre, 80);
        }

        RefreshSiteScrapeSummary();
    }

    partial void OnLinkUrlChanged(string value)
    {
        if (SelectedWebLink is not null || string.IsNullOrWhiteSpace(value))
            return;

        var resolved = ResolveWebLink(new ActressLink { Title = LinkTitle, Url = value });
        if (resolved is not null)
            SelectedWebLink = resolved;
    }

    private void ClearEditor()
    {
        LinkTitle = string.Empty;
        LinkUrl = string.Empty;
        LinkAction = ActressLinkAction.Scrape;
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
                "Seleccione el sitio del Link Manager. Los selectores CSS de actriz se configuran ahí (Scraping actriz).";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedWebLink.ScrapeHintsJson))
        {
            SiteScrapeSummary =
                $"«{SelectedWebLink.Nombre}» aún no tiene selectores de scraping de actriz. Ábralos en Link Manager → Scraping actriz y guarde antes de scrapear.";
            return;
        }

        var key = string.IsNullOrWhiteSpace(SelectedWebLink.ScraperKey) ? "css-list" : SelectedWebLink.ScraperKey;
        SiteScrapeSummary =
            $"Usará los selectores del sitio «{SelectedWebLink.Nombre}» (una vez en Link Manager; ScraperKey={key}). En esta actriz solo cambia la URL del perfil.";
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
                        null,
                        null).ConfigureAwait(true);
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
    private async Task AddLinkAsync()
    {
        IsEditorExpanded = true;

        if (string.IsNullOrWhiteSpace(LinkUrl))
        {
            ErrorMessage = "Indique la URL específica de la actriz (p. ej. https://www.teamskeet.com/models/aidra-fox).";
            return;
        }

        // Si pegó la URL primero, intentar asociar el sitio del Link Manager por dominio.
        if (SelectedWebLink is null)
        {
            SelectedWebLink = ResolveWebLink(new ActressLink
            {
                Title = LinkTitle,
                Url = LinkUrl
            });
        }

        if (string.IsNullOrWhiteSpace(LinkTitle))
        {
            LinkTitle = SelectedWebLink?.Nombre is { Length: > 0 } nombre
                ? TruncateTitle(nombre.Trim(), 80)
                : "Enlace";
        }

        if (LinkAction == ActressLinkAction.Scrape && SelectedWebLink is null)
        {
            ErrorMessage =
                "Seleccione el sitio del Link Manager (p. ej. TeamSkeet). " +
                "Sin ese sitio no se pueden usar los selectores de scraping.";
            return;
        }

        if (LinkAction == ActressLinkAction.Scrape
            && SelectedWebLink is not null
            && string.IsNullOrWhiteSpace(SelectedWebLink.ScrapeHintsJson))
        {
            ErrorMessage =
                $"El sitio «{SelectedWebLink.Nombre}» no tiene selectores de scraping de actriz. " +
                "Ábralo en Link Manager → Scraping actriz, configure ListItemSelector/TitleSelector (con espacios, no puntos entre tags) y guarde.";
            return;
        }

        try
        {
            ErrorMessage = null;
            var created = await _actressLinkService.CreateAsync(
                _actress.Id,
                SelectedWebLink?.Id,
                LinkTitle,
                LinkUrl,
                LinkAction,
                LinkNotes,
                null,
                null).ConfigureAwait(true);

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
            ErrorMessage = "Seleccione el sitio/productora del Link Manager para scrapear.";
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
                null,
                null).ConfigureAwait(true);

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
        if (SelectedLink is null || SelectedLink.Action != ActressLinkAction.Scrape)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var site = SelectedWebLink ?? SelectedLink.WebLink;
            var hintsJson = site?.ScrapeHintsJson;
            var scraperKey = site?.ScraperKey ?? "css-list";

            var header = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Scraping «{SelectedLink.Title}»",
                $"ActressId={_actress.Id} · ActressLinkId={SelectedLink.Id} · Action={SelectedLink.Action}",
                $"WebLinkId={site?.Id.ToString() ?? "(null)"} · Sitio={site?.Nombre ?? "(sin sitio)"}",
                $"ScraperKey={scraperKey}",
                $"URL actriz: {SelectedLink.Url}",
                "—— Hints del sitio (JSON; compartidos por todas las actrices) ——",
                string.IsNullOrWhiteSpace(hintsJson)
                    ? "(sin ScrapeHintsJson — configurelos una vez en Link Manager → Scraping actriz)"
                    : hintsJson.Trim(),
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

        if (!_browserLauncher.TryOpenInFirefox(item.SourceUrl))
            ErrorMessage = "No se pudo abrir el video en Firefox.";
    }
}
