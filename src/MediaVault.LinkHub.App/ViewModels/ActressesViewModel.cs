using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ActressesViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IActressService _actressService;
    private readonly IVideoCategoryService _videoCategoryService;
    private readonly IProducerService _producerService;
    private readonly IMediaVaultService _mediaVaultService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAppDialogService _appDialogService;
    private int _thumbnailGeneration;

    public ActressesViewModel(
        IActressService actressService,
        IVideoCategoryService videoCategoryService,
        IProducerService producerService,
        IMediaVaultService mediaVaultService,
        IAppSettingsService appSettingsService,
        IAppDialogService appDialogService)
    {
        _actressService = actressService;
        _videoCategoryService = videoCategoryService;
        _producerService = producerService;
        _mediaVaultService = mediaVaultService;
        _appSettingsService = appSettingsService;
        _appDialogService = appDialogService;
    }

    public string Title => "Actrices";

    public string Subtitle =>
        "Filtro OR dentro de actrices/categorías/productoras; AND entre grupos. Cualquier carpeta.";

    public ObservableCollection<Actress> Actresses { get; } = [];

    public ObservableCollection<ActressFilterTagItem> ActressFilterTags { get; } = [];

    public ObservableCollection<CategoryFilterTagItem> CategoryFilterTags { get; } = [];

    public ObservableCollection<ProducerFilterTagItem> ProducerFilterTags { get; } = [];

    public ObservableCollection<ActressVideoListItem> Videos { get; } = [];

    [ObservableProperty]
    private Actress? _selectedActress;

    [ObservableProperty]
    private string _actressName = string.Empty;

    [ObservableProperty]
    private ActressVideoListItem? _selectedVideo;

    public bool CanEditActress => SelectedActress is not null;

    public bool HasActressFilter => ActressFilterTags.Any(tag => tag.IsSelected);

    public bool HasCategoryFilter => CategoryFilterTags.Any(tag => tag.IsSelected);

    public bool HasProducerFilter => ProducerFilterTags.Any(tag => tag.IsSelected);

    public bool HasActiveFilter => HasActressFilter || HasCategoryFilter || HasProducerFilter;

    public string ResultsSummary
    {
        get
        {
            if (!HasActiveFilter)
                return "Seleccione actrices, categorías y/o productoras (OR dentro de cada grupo).";

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

        Actresses.Clear();
        foreach (var actress in actresses)
            Actresses.Add(actress);

        RebuildActressFilterTags(actresses);
        RebuildCategoryFilterTags(categories);
        RebuildProducerFilterTags(producers);
        NotifyActressCommands();
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

    private async Task OnFilterChangedAsync()
    {
        NotifyFilterStateChanged();
        await RefreshVideosAsync().ConfigureAwait(true);
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
            Videos.Add(item);
            videoItems.Add(item);
        }

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
                        120)).ConfigureAwait(true);

                if (thumbnail is null)
                {
                    var iconPath = await _appSettingsService.GetFolderIconPathAsync(folderPath).ConfigureAwait(true);
                    if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                        thumbnail = await Task.Run(() => LocalImageLoader.TryLoad(iconPath, 120)).ConfigureAwait(true);
                    else
                        thumbnail = await WindowsShellThumbnailProvider
                            .GetThumbnailAsync(folderPath, isDirectory: true, 120)
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

    partial void OnSelectedActressChanged(Actress? value)
    {
        ActressName = value?.Name ?? string.Empty;
        NotifyActressCommands();
    }

    private void NotifyActressCommands()
    {
        OnPropertyChanged(nameof(CanEditActress));
        RenameActressCommand.NotifyCanExecuteChanged();
        DeleteActressCommand.NotifyCanExecuteChanged();
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
            SelectedActress = null;
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
        if (SelectedActress is null || string.IsNullOrWhiteSpace(ActressName))
            return;

        try
        {
            ErrorMessage = null;
            await _actressService.UpdateAsync(SelectedActress.Id, ActressName).ConfigureAwait(true);
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
        if (SelectedActress is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar la actriz «{SelectedActress.Name}»?\n\nSe quitará de todos los videos.",
                AppDialogKind.Question))
            return;

        try
        {
            ErrorMessage = null;
            await _actressService.DeleteAsync(SelectedActress.Id).ConfigureAwait(true);
            SelectedActress = null;
            ActressName = string.Empty;
            await ReloadCatalogAsync().ConfigureAwait(true);
            await RefreshVideosAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
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
