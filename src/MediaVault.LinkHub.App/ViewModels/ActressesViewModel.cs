using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.App.Views;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ActressesViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IActressService _actressService;
    private readonly IActressLinkService _actressLinkService;
    private readonly IVideoScrapeService _videoScrapeService;
    private readonly IWebLinkService _webLinkService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IVideoCategoryService _videoCategoryService;
    private readonly IProducerService _producerService;
    private readonly IMediaVaultService _mediaVaultService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAppDialogService _appDialogService;
    private int _thumbnailGeneration;
    private int _catalogThumbGeneration;
    private ActressLinksWindow? _openLinksWindow;
    private int? _openLinksActressId;
    private bool _suppressLinksOpen;

    public ActressesViewModel(
        IActressService actressService,
        IActressLinkService actressLinkService,
        IVideoScrapeService videoScrapeService,
        IWebLinkService webLinkService,
        IHttpClientFactory httpClientFactory,
        IVideoCategoryService videoCategoryService,
        IProducerService producerService,
        IMediaVaultService mediaVaultService,
        IAppSettingsService appSettingsService,
        IAppDialogService appDialogService)
    {
        _actressService = actressService;
        _actressLinkService = actressLinkService;
        _videoScrapeService = videoScrapeService;
        _webLinkService = webLinkService;
        _httpClientFactory = httpClientFactory;
        _videoCategoryService = videoCategoryService;
        _producerService = producerService;
        _mediaVaultService = mediaVaultService;
        _appSettingsService = appSettingsService;
        _appDialogService = appDialogService;
    }

    public string Title => "Actrices";

    public string Subtitle =>
        "Filtros primero; catálogo con miniaturas; enlaces en ventana al seleccionar; videos locales en grid.";

    public ObservableCollection<ActressCatalogItem> Catalog { get; } = [];

    public ObservableCollection<ActressFilterTagItem> ActressFilterTags { get; } = [];

    public ObservableCollection<CategoryFilterTagItem> CategoryFilterTags { get; } = [];

    public ObservableCollection<ProducerFilterTagItem> ProducerFilterTags { get; } = [];

    public ObservableCollection<ActressVideoListItem> Videos { get; } = [];

    [ObservableProperty]
    private ActressCatalogItem? _selectedCatalogItem;

    [ObservableProperty]
    private string _actressName = string.Empty;

    [ObservableProperty]
    private ActressVideoListItem? _selectedVideo;

    [ObservableProperty]
    private DateTime? _seenFromDate;

    [ObservableProperty]
    private DateTime? _seenToDate;

    [ObservableProperty]
    private bool _onlyNeverOpened;

    private List<ActressVideoListItem> _allFilteredVideos = [];

    public bool CanEditActress => SelectedCatalogItem is not null;

    public bool HasActressFilter => ActressFilterTags.Any(tag => tag.IsSelected);

    public bool HasCategoryFilter => CategoryFilterTags.Any(tag => tag.IsSelected);

    public bool HasProducerFilter => ProducerFilterTags.Any(tag => tag.IsSelected);

    public bool HasActiveFilter => HasActressFilter || HasCategoryFilter || HasProducerFilter;

    public string ResultsSummary
    {
        get
        {
            if (!HasActiveFilter)
                return "Seleccione filtros (OR dentro de cada grupo; AND entre grupos).";

            return Videos.Count == 1 ? "1 video" : $"{Videos.Count} videos";
        }
    }

    public Task InitializeAsync() =>
        RunBusyCoreAsync(async () =>
        {
            await ReloadCatalogAsync().ConfigureAwait(true);
            await RefreshVideosAsync().ConfigureAwait(true);
        }, "Cargando actrices...");

    private async Task ReloadCatalogAsync()
    {
        var actresses = await _actressService.GetAllAsync().ConfigureAwait(true);
        var categories = await _videoCategoryService.GetAllAsync().ConfigureAwait(true);
        var producers = await _producerService.GetAllAsync().ConfigureAwait(true);

        var previousId = SelectedCatalogItem?.Id;
        Catalog.Clear();
        foreach (var actress in actresses)
            Catalog.Add(new ActressCatalogItem { Actress = actress });

        RebuildActressFilterTags(actresses);
        RebuildCategoryFilterTags(categories);
        RebuildProducerFilterTags(producers);

        _suppressLinksOpen = true;
        try
        {
            SelectedCatalogItem = previousId is int id
                ? Catalog.FirstOrDefault(item => item.Id == id)
                : null;
        }
        finally
        {
            _suppressLinksOpen = false;
        }

        NotifyActressCommands();
        _ = LoadCatalogThumbnailsAsync();
    }

    private async Task LoadCatalogThumbnailsAsync()
    {
        var generation = ++_catalogThumbGeneration;
        var items = Catalog.ToList();

        foreach (var item in items)
        {
            if (generation != _catalogThumbGeneration)
                return;

            try
            {
                var files = await _mediaVaultService
                    .FindVideosByActressIdsAsync([item.Id])
                    .ConfigureAwait(true);

                if (files.Count == 0)
                    continue;

                var rng = Random.Shared;
                var sample = files.OrderBy(_ => rng.Next()).Take(Math.Min(8, files.Count)).ToList();
                ImageSource? thumbnail = null;

                foreach (var file in sample)
                {
                    var pictures = await _mediaVaultService
                        .ListPicturesForVideoAsync(file.Path)
                        .ConfigureAwait(true);

                    if (pictures.Count == 0)
                        continue;

                    var picturePath = pictures[rng.Next(pictures.Count)];
                    thumbnail = await Task.Run(() => LocalImageLoader.TryLoad(picturePath, 160))
                        .ConfigureAwait(true);
                    if (thumbnail is not null)
                        break;
                }

                if (generation != _catalogThumbGeneration)
                    return;

                item.Thumbnail = thumbnail;
            }
            catch
            {
                // Miniatura opcional.
            }
        }
    }

    private void RebuildActressFilterTags(IReadOnlyList<Actress> actresses)
    {
        var previouslySelected = ActressFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.ActressId)
            .ToHashSet();

        ActressFilterTags.Clear();
        foreach (var actress in actresses)
        {
            var tag = new ActressFilterTagItem
            {
                ActressId = actress.Id,
                Name = actress.Name,
                IsSelected = previouslySelected.Contains(actress.Id)
            };
            tag.SelectionChanged = () => _ = OnFilterChangedAsync();
            ActressFilterTags.Add(tag);
        }

        NotifyFilterStateChanged();
    }

    private void RebuildCategoryFilterTags(IReadOnlyList<VideoCategory> categories)
    {
        var previouslySelected = CategoryFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.CategoryId)
            .ToHashSet();

        CategoryFilterTags.Clear();
        foreach (var category in categories)
        {
            var tag = new CategoryFilterTagItem
            {
                CategoryId = category.Id,
                Name = category.Name,
                IsSelected = previouslySelected.Contains(category.Id)
            };
            tag.SelectionChanged = () => _ = OnFilterChangedAsync();
            CategoryFilterTags.Add(tag);
        }

        NotifyFilterStateChanged();
    }

    private void RebuildProducerFilterTags(IReadOnlyList<Producer> producers)
    {
        var previouslySelected = ProducerFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.ProducerId)
            .ToHashSet();

        ProducerFilterTags.Clear();
        foreach (var producer in producers)
        {
            var tag = new ProducerFilterTagItem
            {
                ProducerId = producer.Id,
                Name = producer.Name,
                IsSelected = previouslySelected.Contains(producer.Id)
            };
            tag.SelectionChanged = () => _ = OnFilterChangedAsync();
            ProducerFilterTags.Add(tag);
        }

        NotifyFilterStateChanged();
    }

    private void NotifyFilterStateChanged()
    {
        OnPropertyChanged(nameof(HasActressFilter));
        OnPropertyChanged(nameof(HasCategoryFilter));
        OnPropertyChanged(nameof(HasProducerFilter));
        OnPropertyChanged(nameof(HasActiveFilter));
        OnPropertyChanged(nameof(ResultsSummary));
    }

    private Task OnFilterChangedAsync()
    {
        NotifyFilterStateChanged();
        return RefreshVideosAsync();
    }

    private async Task RefreshVideosAsync()
    {
        var actressIds = ActressFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.ActressId)
            .ToList();

        var categoryIds = CategoryFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.CategoryId)
            .ToList();

        var producerIds = ProducerFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.ProducerId)
            .ToList();

        Videos.Clear();
        SelectedVideo = null;
        _allFilteredVideos = [];

        if (actressIds.Count == 0 && categoryIds.Count == 0 && producerIds.Count == 0)
        {
            OnPropertyChanged(nameof(ResultsSummary));
            return;
        }

        var files = await _mediaVaultService
            .FindVideosByFiltersAsync(actressIds, categoryIds, producerIds)
            .ConfigureAwait(true);
        var generation = ++_thumbnailGeneration;

        var videoItems = new List<ActressVideoListItem>(files.Count);
        foreach (var file in files)
        {
            var item = new ActressVideoListItem { MediaFile = file };
            videoItems.Add(item);
        }

        _allFilteredVideos = videoItems;
        ApplySeenDateFilter();

        await VideoThumbnailSessionBootstrap
            .PrefetchWithDedicatedAsync(
                _mediaVaultService,
                videoItems
                    .Where(item => !string.IsNullOrWhiteSpace(item.FolderPath))
                    .Select(item => (ItemKey: item.MediaFile.Path, FolderPath: item.FolderPath)))
            .ConfigureAwait(true);

        foreach (var item in videoItems)
            _ = LoadThumbnailAsync(item, generation);

        OnPropertyChanged(nameof(ResultsSummary));
    }

    private void ApplySeenDateFilter()
    {
        Videos.Clear();
        SelectedVideo = null;

        IEnumerable<ActressVideoListItem> query = _allFilteredVideos;

        if (OnlyNeverOpened)
        {
            query = query.Where(item =>
                item.MediaFile.LastOpenedAt is null && item.MediaFile.VecesAbierto <= 0);
        }
        else
        {
            if (SeenFromDate is DateTime from)
            {
                var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Local).ToUniversalTime();
                query = query.Where(item =>
                    item.MediaFile.LastOpenedAt is DateTime opened && opened >= fromUtc);
            }

            if (SeenToDate is DateTime to)
            {
                var toExclusive = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
                query = query.Where(item =>
                    item.MediaFile.LastOpenedAt is DateTime opened && opened < toExclusive);
            }
        }

        foreach (var item in query)
            Videos.Add(item);

        OnPropertyChanged(nameof(ResultsSummary));
    }

    partial void OnSeenFromDateChanged(DateTime? value) => ApplySeenDateFilter();

    partial void OnSeenToDateChanged(DateTime? value) => ApplySeenDateFilter();

    partial void OnOnlyNeverOpenedChanged(bool value) => ApplySeenDateFilter();

    [RelayCommand]
    private void ClearSeenDateFilter()
    {
        SeenFromDate = null;
        SeenToDate = null;
        OnlyNeverOpened = false;
        ApplySeenDateFilter();
    }

    private async Task LoadThumbnailAsync(ActressVideoListItem item, int generation)
    {
        var folderPath = item.FolderPath;
        ImageSource? thumbnail = null;

        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            try
            {
                folderPath = Path.GetFullPath(folderPath);
                thumbnail = await Task.Run(() =>
                    FolderSessionPicturePicker.TryLoadThumbnailForItem(
                        folderPath,
                        item.MediaFile.Path,
                        140)).ConfigureAwait(true);

                if (thumbnail is null)
                {
                    var iconPath = await _appSettingsService.GetFolderIconPathAsync(folderPath).ConfigureAwait(true);
                    if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                        thumbnail = await Task.Run(() => LocalImageLoader.TryLoad(iconPath, 140)).ConfigureAwait(true);
                    else
                        thumbnail = await WindowsShellThumbnailProvider
                            .GetThumbnailAsync(folderPath, isDirectory: true, 140)
                            .ConfigureAwait(true);
                }
            }
            catch
            {
                thumbnail = null;
            }
        }

        if (generation != _thumbnailGeneration)
            return;

        item.Thumbnail = thumbnail;
    }

    partial void OnSelectedCatalogItemChanged(ActressCatalogItem? value)
    {
        ActressName = value?.Name ?? string.Empty;
        NotifyActressCommands();

        if (!_suppressLinksOpen && value is not null)
            OpenLinksWindow(value.Actress);
    }

    private void OpenLinksWindow(Actress actress)
    {
        if (_openLinksWindow is { IsLoaded: true } && _openLinksActressId == actress.Id)
        {
            _openLinksWindow.Activate();
            return;
        }

        _openLinksWindow?.Close();

        var vm = new ActressLinksViewModel(
            actress,
            _actressLinkService,
            _videoScrapeService,
            _webLinkService,
            _httpClientFactory,
            _appDialogService);

        var window = new ActressLinksWindow(vm)
        {
            Owner = System.Windows.Application.Current.MainWindow
        };

        _openLinksWindow = window;
        _openLinksActressId = actress.Id;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openLinksWindow, window))
            {
                _openLinksWindow = null;
                _openLinksActressId = null;
            }
        };

        window.Show();
    }

    private void NotifyActressCommands()
    {
        OnPropertyChanged(nameof(CanEditActress));
        RenameActressCommand.NotifyCanExecuteChanged();
        DeleteActressCommand.NotifyCanExecuteChanged();
        OpenSelectedActressLinksCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task AddActressAsync()
    {
        if (string.IsNullOrWhiteSpace(ActressName))
        {
            ErrorMessage = "Indique un nombre para la actriz.";
            return;
        }

        try
        {
            ErrorMessage = null;
            await _actressService.CreateAsync(ActressName).ConfigureAwait(true);
            ActressName = string.Empty;
            SelectedCatalogItem = null;
            await ReloadCatalogAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditActress))]
    private async Task RenameActressAsync()
    {
        if (SelectedCatalogItem is null || string.IsNullOrWhiteSpace(ActressName))
            return;

        try
        {
            ErrorMessage = null;
            await _actressService.UpdateAsync(SelectedCatalogItem.Id, ActressName).ConfigureAwait(true);
            await ReloadCatalogAsync().ConfigureAwait(true);
            await RefreshVideosAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditActress))]
    private async Task DeleteActressAsync()
    {
        if (SelectedCatalogItem is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar la actriz «{SelectedCatalogItem.Name}»?\n\nSe quitará de todos los videos y se borrarán sus enlaces/scrapeos.",
                AppDialogKind.Question))
            return;

        try
        {
            ErrorMessage = null;
            await _actressService.DeleteAsync(SelectedCatalogItem.Id).ConfigureAwait(true);
            SelectedCatalogItem = null;
            ActressName = string.Empty;
            await ReloadCatalogAsync().ConfigureAwait(true);
            await RefreshVideosAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditActress))]
    private void OpenSelectedActressLinks()
    {
        if (SelectedCatalogItem is null)
            return;

        OpenLinksWindow(SelectedCatalogItem.Actress);
    }

    partial void OnSelectedVideoChanged(ActressVideoListItem? value)
    {
        OpenSelectedVideoCommand.NotifyCanExecuteChanged();
        OpenSelectedVideoWithVlcCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedVideo))]
    private async Task OpenSelectedVideoAsync()
    {
        if (SelectedVideo is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(SelectedVideo.MediaFile.Id).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo.");

            await RefreshVideosAsync().ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedVideo))]
    private async Task OpenSelectedVideoWithVlcAsync()
    {
        if (SelectedVideo is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService
                .OpenFileAsync(SelectedVideo.MediaFile.Id, preferVlc: true)
                .ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            await RefreshVideosAsync().ConfigureAwait(true);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    private bool HasSelectedVideo() => SelectedVideo is not null;
}
