using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.MediaVault;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Media;

using Microsoft.Win32;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class MediaVaultViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IMediaVaultService _mediaVaultService;
    private readonly IVideoCategoryService _videoCategoryService;
    private readonly IActressService _actressService;
    private readonly IProducerService _producerService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAppDialogService _appDialogService;
    private readonly BrowserThumbnailLoader _thumbnailLoader;
    private List<MediaVaultBrowserEntry> _directoryEntries = [];
    private int _thumbnailGeneration;
    private bool _suppressRankingPersistence;
    private bool _suppressCategoryPersistence;
    private bool _suppressActressPersistence;
    private bool _suppressProducerPersistence;
    private bool _suppressShowHiddenPersistence;

    public MediaVaultViewModel(
        IMediaVaultService mediaVaultService,
        IVideoCategoryService videoCategoryService,
        IActressService actressService,
        IProducerService producerService,
        IAppSettingsService appSettingsService,
        IAppDialogService appDialogService,
        BrowserThumbnailLoader thumbnailLoader)
    {
        _mediaVaultService = mediaVaultService;
        _videoCategoryService = videoCategoryService;
        _actressService = actressService;
        _producerService = producerService;
        _appSettingsService = appSettingsService;
        _appDialogService = appDialogService;
        _thumbnailLoader = thumbnailLoader;

        SortOptions =
        [
            new BrowserSortOption("Nombre", MediaVaultBrowserSortField.Name),
            new BrowserSortOption("Fecha de creación", MediaVaultBrowserSortField.Created),
            new BrowserSortOption("Fecha de actualización", MediaVaultBrowserSortField.Modified),
            new BrowserSortOption("Tipo de archivo", MediaVaultBrowserSortField.FileType),
            new BrowserSortOption("Veces abierto", MediaVaultBrowserSortField.OpenCount)
        ];

        SortDirections =
        [
            new BrowserSortDirectionOption("Ascendente", true),
            new BrowserSortDirectionOption("Descendente", false)
        ];

        SelectedSortDirection = SortDirections[0];
        SelectedSortOption = SortOptions[0];

        MediaTypeFilters =
        [
            new BrowserMediaTypeFilterOption("Todos", BrowserMediaTypeFilter.All),
            new BrowserMediaTypeFilterOption("Carpetas", BrowserMediaTypeFilter.Directories),
            new BrowserMediaTypeFilterOption("Imágenes", BrowserMediaTypeFilter.Images),
            new BrowserMediaTypeFilterOption("Videos", BrowserMediaTypeFilter.Videos)
        ];

        SelectedMediaTypeFilter = MediaTypeFilters[0];
    }

    public ObservableCollection<FileCategorySelectionItem> FileCategorySelections { get; } = [];

    public ObservableCollection<FileActressSelectionItem> FileActressSelections { get; } = [];

    public ObservableCollection<FileProducerSelectionItem> FileProducerSelections { get; } = [];

    public ObservableCollection<VideoThumbnailListItem> AssignedVideoThumbnails { get; } = [];

    public string Title => "File & Media Vault";

    public string Subtitle => "Exploración y gestión de archivos multimedia locales";

    public ObservableCollection<MediaVaultBrowserEntryItem> BrowserEntries { get; } = [];

    public ObservableCollection<BrowserSortOption> SortOptions { get; }

    public ObservableCollection<BrowserSortDirectionOption> SortDirections { get; }

    public ObservableCollection<BrowserMediaTypeFilterOption> MediaTypeFilters { get; }

    [ObservableProperty]
    private BrowserSortOption? _selectedSortOption;

    [ObservableProperty]
    private BrowserSortDirectionOption? _selectedSortDirection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveBrowserFilters))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveBrowserFilters))]
    private BrowserMediaTypeFilterOption? _selectedMediaTypeFilter;

    [ObservableProperty]
    private bool _showHiddenFilesAndFolders;

    [ObservableProperty]
    private MediaVaultBrowserEntryItem? _selectedBrowserEntryItem;

    [ObservableProperty]
    private MediaFile? _selectedMediaFile;

    [ObservableProperty]
    private string _indexRootPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateFolder))]
    [NotifyCanExecuteChangedFor(nameof(CreateFolderCommand))]
    private string _currentDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _newFileName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateFolder))]
    [NotifyCanExecuteChangedFor(nameof(CreateFolderCommand))]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedVideoResolution))]
    [NotifyPropertyChangedFor(nameof(SelectedVideoResolutionDisplay))]
    private string? _selectedVideoResolutionLabel;

    public bool HasSelectedVideoResolution => !string.IsNullOrWhiteSpace(SelectedVideoResolutionLabel);

    public string SelectedVideoResolutionDisplay => HasSelectedVideoResolution
        ? SelectedVideoResolutionLabel!
        : "No disponible";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankingGlobal))]
    [NotifyPropertyChangedFor(nameof(RankingGlobalStars))]
    [NotifyPropertyChangedFor(nameof(HasFileRanking))]
    private int _rankingCalidad;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankingGlobal))]
    [NotifyPropertyChangedFor(nameof(RankingGlobalStars))]
    [NotifyPropertyChangedFor(nameof(HasFileRanking))]
    private int _rankingContenido;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankingGlobal))]
    [NotifyPropertyChangedFor(nameof(RankingGlobalStars))]
    [NotifyPropertyChangedFor(nameof(HasFileRanking))]
    private int _rankingGusto;

    [ObservableProperty]
    private double? _currentDirectoryAverageRanking;

    [ObservableProperty]
    private int _currentDirectoryRankedFileCount;

    [ObservableProperty]
    private double? _selectedFolderAverageRanking;

    [ObservableProperty]
    private int _selectedFolderRankedFileCount;

    [ObservableProperty]
    private string? _selectedFolderIconPath;

    public MediaVaultBrowserEntry? SelectedBrowserEntry => SelectedBrowserEntryItem?.Entry;

    public bool CanNavigateUp =>
        !string.IsNullOrWhiteSpace(IndexRootPath)
        && !string.IsNullOrWhiteSpace(CurrentDirectoryPath)
        && !string.Equals(
            Path.GetFullPath(CurrentDirectoryPath),
            Path.GetFullPath(IndexRootPath),
            StringComparison.OrdinalIgnoreCase);

    public bool CanEditSelectedFile => SelectedMediaFile is not null;

    public bool ShowFileCategorySection => SelectedMediaFile is not null;

    public bool ShowFileActressSection =>
        SelectedMediaFile is not null
        && MediaFileExtensions.IsVideo(SelectedMediaFile.Path);

    public bool ShowVideoResolutionSection =>
        SelectedBrowserEntry is { IsDirectory: false } entry
        && MediaFileExtensions.IsVideo(entry.FullPath);

    public bool CanAssignVideoCategory => CanEditSelectedFile;

    public bool CanAssignActress => CanEditSelectedFile && ShowFileActressSection;

    public bool ShowFileProducerSection => ShowFileActressSection;

    public bool ShowVideoThumbnailSection => ShowFileActressSection;

    public bool CanAssignProducer => CanEditSelectedFile && ShowFileProducerSection;

    public bool CanManageVideoThumbnails => CanEditSelectedFile && ShowVideoThumbnailSection;

    public bool HasAssignedVideoThumbnails => AssignedVideoThumbnails.Count > 0;

    public string VideoThumbnailHint =>
        HasAssignedVideoThumbnails
            ? "El Dashboard y el Vault eligen al azar una de estas fotos (estable en la sesión)."
            : "Sin asignación: se usa el pool compartido de la carpeta Pictures.";

    public string FileActressHint =>
        FileActressSelections.Count == 0
            ? "Cree actrices en el módulo Actrices para asignarlas aquí."
            : "Pulse para asignar o quitar. Filtro global OR en la vista Actrices.";

    public string FileProducerHint =>
        FileProducerSelections.Count == 0
            ? "Cree productoras en el módulo Productoras para asignarlas aquí."
            : "Pulse para asignar o quitar. Filtro global OR en la vista Actrices.";

    public string FileCategoryHint =>
        "Pulse un tag para asignar o quitar. Gestione la lista en el módulo «Categorías».";

    public bool CanDeleteSelectedEntry => SelectedBrowserEntryItem is not null
        && !(SelectedBrowserEntryItem.IsDirectory
             && !string.IsNullOrWhiteSpace(IndexRootPath)
             && string.Equals(
                 Path.GetFullPath(SelectedBrowserEntryItem.FullPath),
                 Path.GetFullPath(IndexRootPath),
                 StringComparison.OrdinalIgnoreCase));

    public bool CanMoveSelectedFile => SelectedBrowserEntryItem is { IsDirectory: false }
        && !string.IsNullOrWhiteSpace(IndexRootPath);

    public bool CanCreateFolder =>
        !string.IsNullOrWhiteSpace(CurrentDirectoryPath)
        && Directory.Exists(CurrentDirectoryPath)
        && !string.IsNullOrWhiteSpace(NewFolderName);

    public bool IsSelectedFolder => SelectedBrowserEntry?.IsDirectory == true;

    public bool CanClearFolderIcon =>
        IsSelectedFolder
        && (!string.IsNullOrWhiteSpace(SelectedBrowserEntry?.CustomIconPath)
            || !string.IsNullOrWhiteSpace(SelectedFolderIconPath));

    public bool IsSelectedFile => SelectedBrowserEntry is { IsDirectory: false };

    public double RankingGlobal =>
        MediaFileRankingScale.ComputeGlobal(RankingCalidad, RankingContenido, RankingGusto);

    public int RankingGlobalStars => MediaFileRankingScale.ToDisplayStars(RankingGlobal);

    public bool HasFileRanking => RankingGlobal > 0;

    public bool HasCurrentDirectoryRanking => CurrentDirectoryAverageRanking.HasValue;

    public bool HasSelectedFolderRanking => SelectedFolderAverageRanking.HasValue;

    public int CurrentDirectoryAverageRankingStars =>
        MediaFileRankingScale.ToDisplayStars(CurrentDirectoryAverageRanking ?? 0);

    public int SelectedFolderAverageRankingStars =>
        MediaFileRankingScale.ToDisplayStars(SelectedFolderAverageRanking ?? 0);

    public bool HasActiveBrowserFilters =>
        !string.IsNullOrWhiteSpace(SearchText)
        || SelectedMediaTypeFilter?.Kind != BrowserMediaTypeFilter.All;

    public string BrowserFilterSummary
    {
        get
        {
            var total = _directoryEntries.Count;
            var visible = BrowserEntries.Count;

            return visible == total && !HasActiveBrowserFilters
                ? $"{total} elementos"
                : $"{visible} de {total} elementos";
        }
    }

    public async Task InitializeAsync()
    {
        await LoadVideoCategoriesAsync().ConfigureAwait(true);
        await LoadActressesAsync().ConfigureAwait(true);
        await LoadProducersAsync().ConfigureAwait(true);
        await LoadIndexRootPathAsync().ConfigureAwait(true);
        await BrowseDirectoryAsync(IndexRootPath).ConfigureAwait(true);
    }

    private async Task LoadVideoCategoriesAsync()
    {
        var categories = await _videoCategoryService.GetAllAsync().ConfigureAwait(true);
        RebuildFileCategorySelections(categories, SelectedMediaFile);
    }

    private async Task LoadActressesAsync()
    {
        var actresses = await _actressService.GetAllAsync().ConfigureAwait(true);
        RebuildFileActressSelections(actresses, SelectedMediaFile);
    }

    private async Task LoadProducersAsync()
    {
        var producers = await _producerService.GetAllAsync().ConfigureAwait(true);
        RebuildFileProducerSelections(producers, SelectedMediaFile);
    }

    private void RebuildFileCategorySelections(IReadOnlyList<VideoCategory> categories, MediaFile? selectedFile)
    {
        var selectedIds = selectedFile?.Categories.Select(category => category.Id).ToHashSet() ?? [];

        FileCategorySelections.Clear();
        foreach (var category in categories)
        {
            var item = new FileCategorySelectionItem
            {
                CategoryId = category.Id,
                Name = category.Name,
                IsSelected = selectedIds.Contains(category.Id)
            };
            item.SelectionChanged = () => _ = PersistCategoriesIfNeededAsync();
            FileCategorySelections.Add(item);
        }
    }

    private void SyncFileCategorySelections(MediaFile? mediaFile)
    {
        var selectedIds = mediaFile?.Categories.Select(category => category.Id).ToHashSet() ?? [];

        _suppressCategoryPersistence = true;
        try
        {
            foreach (var item in FileCategorySelections)
                item.IsSelected = selectedIds.Contains(item.CategoryId);
        }
        finally
        {
            _suppressCategoryPersistence = false;
        }
    }

    private void RebuildFileActressSelections(IReadOnlyList<Actress> actresses, MediaFile? selectedFile)
    {
        var selectedIds = selectedFile?.Actresses.Select(actress => actress.Id).ToHashSet() ?? [];

        FileActressSelections.Clear();
        foreach (var actress in actresses)
        {
            var item = new FileActressSelectionItem
            {
                ActressId = actress.Id,
                Name = actress.Name,
                IsSelected = selectedIds.Contains(actress.Id)
            };
            item.SelectionChanged = () => _ = PersistActressesIfNeededAsync();
            FileActressSelections.Add(item);
        }

        OnPropertyChanged(nameof(FileActressHint));
    }

    private void SyncFileActressSelections(MediaFile? mediaFile)
    {
        var selectedIds = mediaFile?.Actresses.Select(actress => actress.Id).ToHashSet() ?? [];

        _suppressActressPersistence = true;
        try
        {
            foreach (var item in FileActressSelections)
                item.IsSelected = selectedIds.Contains(item.ActressId);
        }
        finally
        {
            _suppressActressPersistence = false;
        }
    }

    private void RebuildFileProducerSelections(IReadOnlyList<Producer> producers, MediaFile? selectedFile)
    {
        var selectedIds = selectedFile?.Producers.Select(producer => producer.Id).ToHashSet() ?? [];

        FileProducerSelections.Clear();
        foreach (var producer in producers)
        {
            var item = new FileProducerSelectionItem
            {
                ProducerId = producer.Id,
                Name = producer.Name,
                IsSelected = selectedIds.Contains(producer.Id)
            };
            item.SelectionChanged = () => _ = PersistProducersIfNeededAsync();
            FileProducerSelections.Add(item);
        }

        OnPropertyChanged(nameof(FileProducerHint));
    }

    private void SyncFileProducerSelections(MediaFile? mediaFile)
    {
        var selectedIds = mediaFile?.Producers.Select(producer => producer.Id).ToHashSet() ?? [];

        _suppressProducerPersistence = true;
        try
        {
            foreach (var item in FileProducerSelections)
                item.IsSelected = selectedIds.Contains(item.ProducerId);
        }
        finally
        {
            _suppressProducerPersistence = false;
        }
    }

    private async Task LoadIndexRootPathAsync()
    {
        var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);
        IndexRootPath = settings.MediaIndexRootPath;
        _suppressShowHiddenPersistence = true;
        ShowHiddenFilesAndFolders = settings.ShowHiddenFilesAndFolders;
        _suppressShowHiddenPersistence = false;
    }

    partial void OnShowHiddenFilesAndFoldersChanged(bool value)
    {
        if (_suppressShowHiddenPersistence)
            return;

        _ = PersistShowHiddenAndReloadAsync(value);
    }

    private async Task PersistShowHiddenAndReloadAsync(bool value)
    {
        try
        {
            var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);
            await _appSettingsService.SaveAsync(new Application.Models.Settings.AppSettings
            {
                MediaIndexRootPath = settings.MediaIndexRootPath,
                FolderIconPaths = settings.FolderIconPaths,
                ShowHiddenFilesAndFolders = value
            }).ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(CurrentDirectoryPath))
            {
                await BrowseDirectoryAsync(
                    CurrentDirectoryPath,
                    SelectedMediaFile?.Id,
                    SelectedBrowserEntryItem?.FullPath).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task BrowseDirectoryAsync(string? directoryPath, int? reselectMediaFileId = null, string? reselectEntryPath = null)
    {
        if (string.IsNullOrWhiteSpace(IndexRootPath) || string.IsNullOrWhiteSpace(directoryPath))
        {
            BrowserEntries.Clear();
            CurrentDirectoryPath = string.Empty;
            SelectedBrowserEntryItem = null;
            SelectedMediaFile = null;
            NotifyNavigationChanged();
            return;
        }

        if (!Directory.Exists(directoryPath))
        {
            ErrorMessage = "La carpeta configurada no existe. Actualícela en Configuración.";
            BrowserEntries.Clear();
            CurrentDirectoryPath = string.Empty;
            NotifyNavigationChanged();
            return;
        }

        var entries = await _mediaVaultService
            .ListDirectoryEntriesAsync(directoryPath, IndexRootPath, ShowHiddenFilesAndFolders)
            .ConfigureAwait(true);

        var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);

        CurrentDirectoryPath = Path.GetFullPath(directoryPath);
        _directoryEntries = EnrichEntriesWithFolderIcons(entries, settings);
        ApplyCurrentDirectoryRanking(_directoryEntries);
        _thumbnailGeneration = _thumbnailLoader.NextGeneration();
        ApplySortToBrowser(reselectMediaFileId, reselectEntryPath);
        NotifyNavigationChanged();
    }

    private void ApplyCurrentDirectoryRanking(IReadOnlyList<MediaVaultBrowserEntry> entries)
    {
        var stats = MediaVaultDirectoryRanking.FromEntries(entries);
        CurrentDirectoryAverageRanking = stats.AverageGlobal;
        CurrentDirectoryRankedFileCount = stats.RankedFileCount;
        OnPropertyChanged(nameof(HasCurrentDirectoryRanking));
        OnPropertyChanged(nameof(CurrentDirectoryAverageRankingStars));

        if (SelectedBrowserEntryItem is { IsDirectory: true } selectedFolder
            && string.Equals(
                Path.GetFullPath(selectedFolder.FullPath),
                CurrentDirectoryPath,
                StringComparison.OrdinalIgnoreCase))
        {
            ApplySelectedFolderRanking(stats);
        }
    }

    private void ApplySelectedFolderRanking(MediaVaultDirectoryRanking stats)
    {
        SelectedFolderAverageRanking = stats.AverageGlobal;
        SelectedFolderRankedFileCount = stats.RankedFileCount;
        OnPropertyChanged(nameof(HasSelectedFolderRanking));
        OnPropertyChanged(nameof(SelectedFolderAverageRankingStars));
    }

    private async Task RefreshSelectedFolderRankingAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(IndexRootPath))
        {
            ApplySelectedFolderRanking(MediaVaultDirectoryRanking.Empty);
            return;
        }

        if (string.Equals(
                Path.GetFullPath(folderPath),
                Path.GetFullPath(CurrentDirectoryPath),
                StringComparison.OrdinalIgnoreCase))
        {
            ApplySelectedFolderRanking(MediaVaultDirectoryRanking.FromEntries(_directoryEntries));
            return;
        }

        try
        {
            var entries = await _mediaVaultService
                .ListDirectoryEntriesAsync(folderPath, IndexRootPath, ShowHiddenFilesAndFolders)
                .ConfigureAwait(true);
            ApplySelectedFolderRanking(MediaVaultDirectoryRanking.FromEntries(entries));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ApplySelectedFolderRanking(MediaVaultDirectoryRanking.Empty);
        }
    }

    private void QueueThumbnailLoads()
    {
        _thumbnailLoader.BeginLoad(
            _thumbnailGeneration,
            BrowserEntries.Where(item => item.Thumbnail is null));
    }

    private static List<MediaVaultBrowserEntry> EnrichEntriesWithFolderIcons(
        IReadOnlyList<MediaVaultBrowserEntry> entries,
        Application.Models.Settings.AppSettings settings)
    {
        var result = new List<MediaVaultBrowserEntry>(entries.Count);

        foreach (var entry in entries)
        {
            if (!entry.IsDirectory)
            {
                result.Add(entry);
                continue;
            }

            var folderKey = Path.GetFullPath(entry.FullPath);
            if (!settings.FolderIconPaths.TryGetValue(folderKey, out var iconPath)
                || string.IsNullOrWhiteSpace(iconPath)
                || !File.Exists(iconPath))
            {
                result.Add(entry);
                continue;
            }

            result.Add(new MediaVaultBrowserEntry
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                IsDirectory = true,
                FileType = entry.FileType,
                CreatedAtUtc = entry.CreatedAtUtc,
                ModifiedAtUtc = entry.ModifiedAtUtc,
                CustomIconPath = Path.GetFullPath(iconPath)
            });
        }

        return result;
    }

    partial void OnSelectedSortOptionChanged(BrowserSortOption? value)
    {
        if (value?.Field == MediaVaultBrowserSortField.OpenCount
            && SortDirections.Count > 1
            && SelectedSortDirection?.IsAscending == true)
        {
            SelectedSortDirection = SortDirections.FirstOrDefault(option => !option.IsAscending)
                ?? SelectedSortDirection;
        }

        ApplySortToBrowser();
    }

    partial void OnSelectedSortDirectionChanged(BrowserSortDirectionOption? value) =>
        ApplySortToBrowser();

    partial void OnSearchTextChanged(string value) =>
        ApplySortToBrowser();

    partial void OnSelectedMediaTypeFilterChanged(BrowserMediaTypeFilterOption? value) =>
        ApplySortToBrowser();

    [RelayCommand(CanExecute = nameof(HasActiveBrowserFilters))]
    private void ClearBrowserFilters()
    {
        SearchText = string.Empty;
        SelectedMediaTypeFilter = MediaTypeFilters[0];
        ApplySortToBrowser();
    }

    private void ApplySortToBrowser(int? reselectMediaFileId = null, string? reselectEntryPath = null)
    {
        if (SelectedSortOption is null || SelectedSortDirection is null)
            return;

        var selectedPath = SelectedBrowserEntry?.FullPath;
        var existingThumbnails = BrowserEntries.ToDictionary(
            item => item.FullPath,
            item => item.Thumbnail,
            StringComparer.OrdinalIgnoreCase);

        var sortedEntries = SortEntries(
            ApplyBrowserFilters(_directoryEntries),
            SelectedSortOption.Field,
            SelectedSortDirection.IsAscending);

        BrowserEntries.Clear();
        foreach (var entry in sortedEntries)
        {
            var item = new MediaVaultBrowserEntryItem(entry);
            if (existingThumbnails.TryGetValue(entry.FullPath, out var thumbnail)
                && ShouldPreserveThumbnail(entry, thumbnail))
            {
                item.Thumbnail = thumbnail;
            }

            BrowserEntries.Add(item);
        }

        MediaVaultBrowserEntryItem? selectedItem = null;
        if (reselectMediaFileId.HasValue)
        {
            selectedItem = BrowserEntries.FirstOrDefault(item =>
                item.MediaFile?.Id == reselectMediaFileId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(reselectEntryPath))
        {
            selectedItem = BrowserEntries.FirstOrDefault(item =>
                string.Equals(item.FullPath, reselectEntryPath, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            selectedItem = BrowserEntries.FirstOrDefault(item =>
                string.Equals(item.FullPath, selectedPath, StringComparison.OrdinalIgnoreCase));
        }

        SelectedBrowserEntryItem = selectedItem
            ?? BrowserEntries.FirstOrDefault(item => !item.IsDirectory)
            ?? BrowserEntries.FirstOrDefault();

        QueueThumbnailLoads();
        NotifyBrowserFilterStateChanged();
    }

    private void NotifyBrowserFilterStateChanged()
    {
        OnPropertyChanged(nameof(BrowserFilterSummary));
        OnPropertyChanged(nameof(HasActiveBrowserFilters));
        ClearBrowserFiltersCommand.NotifyCanExecuteChanged();
    }

    private IReadOnlyList<MediaVaultBrowserEntry> ApplyBrowserFilters(
        IReadOnlyList<MediaVaultBrowserEntry> entries) =>
        FilterEntriesBySearch(
                FilterEntriesByMediaType(
                    entries,
                    SelectedMediaTypeFilter?.Kind ?? BrowserMediaTypeFilter.All),
                SearchText)
            .ToList();

    private static bool ShouldPreserveThumbnail(MediaVaultBrowserEntry entry, ImageSource? thumbnail) =>
        thumbnail is not null
        && (!entry.IsDirectory || !string.IsNullOrWhiteSpace(entry.CustomIconPath));

    private void NotifyFolderIconStateChanged()
    {
        OnPropertyChanged(nameof(CanClearFolderIcon));
        ClearFolderIconCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFolderIconPathChanged(string? value) =>
        NotifyFolderIconStateChanged();

    private static IReadOnlyList<MediaVaultBrowserEntry> SortEntries(
        IEnumerable<MediaVaultBrowserEntry> entries,
        MediaVaultBrowserSortField sortField,
        bool ascending)
    {
        IEnumerable<MediaVaultBrowserEntry> query = entries;

        query = sortField switch
        {
            MediaVaultBrowserSortField.Created => ascending
                ? query.OrderBy(entry => entry.CreatedAtUtc ?? DateTime.MinValue)
                : query.OrderByDescending(entry => entry.CreatedAtUtc ?? DateTime.MinValue),
            MediaVaultBrowserSortField.Modified => ascending
                ? query.OrderBy(entry => entry.ModifiedAtUtc ?? DateTime.MinValue)
                : query.OrderByDescending(entry => entry.ModifiedAtUtc ?? DateTime.MinValue),
            MediaVaultBrowserSortField.FileType => ascending
                ? query.OrderBy(entry => entry.FileType, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(entry => entry.FileType, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            MediaVaultBrowserSortField.OpenCount => ascending
                ? query.OrderBy(entry => entry.MediaFile?.VecesAbierto ?? 0)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(entry => entry.MediaFile?.VecesAbierto ?? 0)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase),
            _ => ascending
                ? query.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        };

        return query.ToList();
    }

    private static IEnumerable<MediaVaultBrowserEntry> FilterEntriesByMediaType(
        IEnumerable<MediaVaultBrowserEntry> entries,
        BrowserMediaTypeFilter filter) =>
        filter switch
        {
            BrowserMediaTypeFilter.Directories => entries.Where(entry => entry.IsDirectory),
            BrowserMediaTypeFilter.Images => entries.Where(entry =>
                entry.IsDirectory || MediaFileExtensions.IsImage(entry.FullPath)),
            BrowserMediaTypeFilter.Videos => entries.Where(entry =>
                entry.IsDirectory || MediaFileExtensions.IsVideo(entry.FullPath)),
            _ => entries
        };

    private static IEnumerable<MediaVaultBrowserEntry> FilterEntriesBySearch(
        IEnumerable<MediaVaultBrowserEntry> entries,
        string? searchText)
    {
        var term = searchText?.Trim();
        if (string.IsNullOrEmpty(term))
            return entries;

        return entries.Where(entry => MatchesSearch(entry, term));
    }

    private static bool MatchesSearch(MediaVaultBrowserEntry entry, string term)
    {
        if (entry.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        if (entry.IsDirectory)
            return false;

        if (entry.FileType.Contains(term, StringComparison.OrdinalIgnoreCase))
            return true;

        var extension = Path.GetExtension(entry.FullPath);
        return extension.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyFileCategoryStateChanged()
    {
        OnPropertyChanged(nameof(CanAssignVideoCategory));
        OnPropertyChanged(nameof(ShowFileCategorySection));
        OnPropertyChanged(nameof(FileCategoryHint));
        OnPropertyChanged(nameof(CanAssignActress));
        OnPropertyChanged(nameof(ShowFileActressSection));
        OnPropertyChanged(nameof(ShowVideoResolutionSection));
        OnPropertyChanged(nameof(FileActressHint));
        OnPropertyChanged(nameof(CanAssignProducer));
        OnPropertyChanged(nameof(ShowFileProducerSection));
        OnPropertyChanged(nameof(FileProducerHint));
        OnPropertyChanged(nameof(ShowVideoThumbnailSection));
        OnPropertyChanged(nameof(CanManageVideoThumbnails));
        OnPropertyChanged(nameof(HasAssignedVideoThumbnails));
        OnPropertyChanged(nameof(VideoThumbnailHint));
    }

    private void NotifyNavigationChanged() =>
        OnPropertyChanged(nameof(CanNavigateUp));

    partial void OnSelectedBrowserEntryItemChanged(MediaVaultBrowserEntryItem? value)
    {
        OnPropertyChanged(nameof(SelectedBrowserEntry));
        OnPropertyChanged(nameof(IsSelectedFolder));
        OnPropertyChanged(nameof(IsSelectedFile));

        OnPropertyChanged(nameof(CanDeleteSelectedEntry));
        DeleteCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanMoveSelectedFile));
        MoveCommand.NotifyCanExecuteChanged();
        NotifyFolderIconStateChanged();

        if (value is null)
        {
            SelectedMediaFile = null;
            SelectedFolderIconPath = null;
            NewFileName = string.Empty;
            SelectedVideoResolutionLabel = null;
            RankingCalidad = 0;
            RankingContenido = 0;
            RankingGusto = 0;
            ClearAssignedVideoThumbnails();
            ApplySelectedFolderRanking(MediaVaultDirectoryRanking.Empty);
            OnPropertyChanged(nameof(CanEditSelectedFile));
            NotifyFileCategoryStateChanged();
            return;
        }

        _thumbnailLoader.LoadItem(value, _thumbnailGeneration);

        _suppressRankingPersistence = true;
        try
        {
            if (value.IsDirectory)
            {
                SelectedMediaFile = null;
                SelectedFolderIconPath = value.Entry.CustomIconPath;
                NewFileName = string.Empty;
                SelectedVideoResolutionLabel = null;
                RankingCalidad = 0;
                RankingContenido = 0;
                RankingGusto = 0;
                ClearAssignedVideoThumbnails();
                OnPropertyChanged(nameof(CanEditSelectedFile));
                NotifyFileCategoryStateChanged();
                _ = RefreshSelectedFolderRankingAsync(value.FullPath);
                return;
            }

            ApplySelectedFolderRanking(MediaVaultDirectoryRanking.Empty);

            SelectedFolderIconPath = null;
            SelectedMediaFile = value.MediaFile;
            if (value.MediaFile is null)
            {
                NewFileName = Path.GetFileNameWithoutExtension(value.Name);
                RankingCalidad = 0;
                RankingContenido = 0;
                RankingGusto = 0;
                ClearAssignedVideoThumbnails();
                OnPropertyChanged(nameof(CanEditSelectedFile));
                NotifyFileCategoryStateChanged();
                _ = LoadSelectedVideoResolutionAsync(value.FullPath);

                if (MediaFileExtensions.IsSupported(value.FullPath))
                    _ = EnsureSelectedFileIndexedAsync(value.FullPath);

                return;
            }

            NewFileName = Path.GetFileNameWithoutExtension(value.MediaFile.Name);
            RankingCalidad = MediaFileRankingScale.ToStars(value.MediaFile.RankingCalidad);
            RankingContenido = MediaFileRankingScale.ToStars(value.MediaFile.RankingContenido);
            RankingGusto = MediaFileRankingScale.ToStars(value.MediaFile.RankingGusto);
            SyncFileCategorySelections(value.MediaFile);
            SyncFileActressSelections(value.MediaFile);
            SyncFileProducerSelections(value.MediaFile);
            _ = LoadSelectedVideoResolutionAsync(value.MediaFile.Path);
            _ = LoadAssignedVideoThumbnailsAsync(value.MediaFile.Id, value.MediaFile.Path);

            OnPropertyChanged(nameof(CanEditSelectedFile));
            NotifyFileCategoryStateChanged();
        }
        finally
        {
            _suppressRankingPersistence = false;
        }
    }

    private async Task EnsureSelectedFileIndexedAsync(string filePath)
    {
        try
        {
            ErrorMessage = null;
            var mediaFile = await _mediaVaultService.EnsureIndexedAsync(filePath).ConfigureAwait(true);
            if (SelectedBrowserEntryItem is null
                || !string.Equals(SelectedBrowserEntryItem.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await BrowseDirectoryAsync(CurrentDirectoryPath, reselectMediaFileId: mediaFile.Id).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void ApplyUpdatedMediaFileLocally(MediaFile updated)
    {
        if (SelectedMediaFile?.Id == updated.Id)
        {
            SelectedMediaFile = updated;
            SyncFileCategorySelections(updated);
            SyncFileActressSelections(updated);
            SyncFileProducerSelections(updated);
        }

        for (var index = 0; index < _directoryEntries.Count; index++)
        {
            var entry = _directoryEntries[index];
            if (entry.IsDirectory || entry.MediaFile is null || entry.MediaFile.Id != updated.Id)
                continue;

            _directoryEntries[index] = new MediaVaultBrowserEntry
            {
                Name = entry.Name,
                FullPath = entry.FullPath,
                IsDirectory = false,
                FileType = entry.FileType,
                CreatedAtUtc = entry.CreatedAtUtc,
                ModifiedAtUtc = entry.ModifiedAtUtc,
                MediaFile = updated,
                CustomIconPath = entry.CustomIconPath
            };
            break;
        }

        if (SelectedBrowserEntryItem?.MediaFile?.Id == updated.Id)
        {
            var index = BrowserEntries.IndexOf(SelectedBrowserEntryItem);
            if (index >= 0)
            {
                var refreshed = new MediaVaultBrowserEntryItem(
                    new MediaVaultBrowserEntry
                    {
                        Name = SelectedBrowserEntryItem.Name,
                        FullPath = SelectedBrowserEntryItem.FullPath,
                        IsDirectory = false,
                        FileType = SelectedBrowserEntryItem.FileType,
                        CreatedAtUtc = SelectedBrowserEntryItem.Entry.CreatedAtUtc,
                        ModifiedAtUtc = SelectedBrowserEntryItem.Entry.ModifiedAtUtc,
                        MediaFile = updated,
                        CustomIconPath = SelectedBrowserEntryItem.Entry.CustomIconPath
                    })
                {
                    Thumbnail = SelectedBrowserEntryItem.Thumbnail
                };

                BrowserEntries[index] = refreshed;
                _suppressRankingPersistence = true;
                _suppressCategoryPersistence = true;
                _suppressActressPersistence = true;
                _suppressProducerPersistence = true;
                try
                {
                    SelectedBrowserEntryItem = refreshed;
                }
                finally
                {
                    _suppressRankingPersistence = false;
                    _suppressCategoryPersistence = false;
                    _suppressActressPersistence = false;
                    _suppressProducerPersistence = false;
                }
            }
        }

        ApplyCurrentDirectoryRanking(_directoryEntries);
    }

    partial void OnRankingCalidadChanged(int value) => _ = PersistRankingsIfNeededAsync();

    partial void OnRankingContenidoChanged(int value) => _ = PersistRankingsIfNeededAsync();

    partial void OnRankingGustoChanged(int value) => _ = PersistRankingsIfNeededAsync();

    private async Task PersistCategoriesIfNeededAsync()
    {
        if (_suppressCategoryPersistence || SelectedMediaFile is null || !CanAssignVideoCategory)
            return;

        await SaveFileCategoriesAsync().ConfigureAwait(true);
    }

    private async Task PersistActressesIfNeededAsync()
    {
        if (_suppressActressPersistence || SelectedMediaFile is null || !CanAssignActress)
            return;

        await SaveFileActressesAsync().ConfigureAwait(true);
    }

    private async Task PersistProducersIfNeededAsync()
    {
        if (_suppressProducerPersistence || SelectedMediaFile is null || !CanAssignProducer)
            return;

        await SaveFileProducersAsync().ConfigureAwait(true);
    }

    private async Task PersistRankingsIfNeededAsync()
    {
        if (_suppressRankingPersistence || SelectedMediaFile is null)
            return;

        await SaveRankingsAsync().ConfigureAwait(true);
    }

    partial void OnSelectedMediaFileChanged(MediaFile? value) =>
        OnPropertyChanged(nameof(CanEditSelectedFile));

    [RelayCommand]
    private Task NavigateUpAsync()
    {
        if (!CanNavigateUp)
            return Task.CompletedTask;

        var parentDirectory = Directory.GetParent(CurrentDirectoryPath)?.FullName;
        if (string.IsNullOrWhiteSpace(parentDirectory))
            return Task.CompletedTask;

        return BrowseDirectoryAsync(parentDirectory);
    }

    [RelayCommand]
    private async Task OpenBrowserEntryAsync(MediaVaultBrowserEntryItem? item)
    {
        if (item is null)
            return;

        if (item.IsDirectory)
        {
            await BrowseDirectoryAsync(item.FullPath).ConfigureAwait(true);
            return;
        }

        await OpenBrowserFileAsync(item).ConfigureAwait(true);
    }

    private async Task OpenBrowserFileAsync(MediaVaultBrowserEntryItem item)
    {
        if (!File.Exists(item.FullPath))
        {
            ErrorMessage = "El archivo no existe.";
            return;
        }

        // Multimedia: siempre indexar + OpenFileAsync para incrementar VecesAbierto.
        if (MediaFileExtensions.IsSupported(item.FullPath))
        {
            try
            {
                ErrorMessage = null;
                var mediaFile = item.MediaFile
                    ?? await _mediaVaultService.EnsureIndexedAsync(item.FullPath).ConfigureAwait(true);

                var opened = await _mediaVaultService.OpenFileAsync(mediaFile.Id).ConfigureAwait(true);
                if (opened is null)
                {
                    ErrorMessage = "No se pudo abrir el archivo.";
                    return;
                }

                ApplyUpdatedMediaFileLocally(opened);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            return;
        }

        // Archivos no multimedia: apertura nativa sin contador.
        try
        {
            ErrorMessage = null;
            Process.Start(new ProcessStartInfo
            {
                FileName = item.FullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo abrir el archivo: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseFolderIcon()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Seleccionar icono de carpeta",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.ico;*.bmp|Todos los archivos|*.*"
        };

        if (!string.IsNullOrWhiteSpace(SelectedFolderIconPath) && File.Exists(SelectedFolderIconPath))
            dialog.InitialDirectory = Path.GetDirectoryName(SelectedFolderIconPath);

        if (dialog.ShowDialog() == true)
            SelectedFolderIconPath = dialog.FileName;
    }

    [RelayCommand]
    private async Task AddVideoThumbnailsAsync()
    {
        if (SelectedMediaFile is null || !CanManageVideoThumbnails)
            return;

        var videoPath = SelectedMediaFile.Path;
        var pictures = await _mediaVaultService.ListPicturesForVideoAsync(videoPath).ConfigureAwait(true);
        var picturesDirectory = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(videoPath)) ?? string.Empty,
            "Pictures");

        var dialog = new OpenFileDialog
        {
            Title = "Asignar miniaturas al video",
            Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif|Todos los archivos|*.*",
            Multiselect = true
        };

        if (Directory.Exists(picturesDirectory))
            dialog.InitialDirectory = picturesDirectory;
        else if (pictures.Count > 0)
            dialog.InitialDirectory = Path.GetDirectoryName(pictures[0]);

        if (dialog.ShowDialog() != true || dialog.FileNames.Length == 0)
            return;

        var merged = AssignedVideoThumbnails
            .Select(item => item.ImagePath)
            .Concat(dialog.FileNames)
            .ToList();

        await PersistVideoThumbnailsAsync(SelectedMediaFile.Id, videoPath, merged).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RemoveVideoThumbnailAsync(VideoThumbnailListItem? item)
    {
        if (item is null || SelectedMediaFile is null || !CanManageVideoThumbnails)
            return;

        var remaining = AssignedVideoThumbnails
            .Where(existing => !string.Equals(existing.ImagePath, item.ImagePath, StringComparison.OrdinalIgnoreCase))
            .Select(existing => existing.ImagePath)
            .ToList();

        await PersistVideoThumbnailsAsync(SelectedMediaFile.Id, SelectedMediaFile.Path, remaining)
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ClearVideoThumbnailsAsync()
    {
        if (SelectedMediaFile is null || !CanManageVideoThumbnails || !HasAssignedVideoThumbnails)
            return;

        await PersistVideoThumbnailsAsync(SelectedMediaFile.Id, SelectedMediaFile.Path, [])
            .ConfigureAwait(true);
    }

    private async Task PersistVideoThumbnailsAsync(
        int mediaFileId,
        string videoPath,
        IReadOnlyCollection<string> imagePaths)
    {
        await ExecuteBusyAsync(async () =>
        {
            ErrorMessage = null;
            var saved = await _mediaVaultService
                .SetThumbnailPathsAsync(mediaFileId, imagePaths)
                .ConfigureAwait(true);

            FolderSessionPicturePicker.RegisterDedicatedPictures(videoPath, saved);
            await ApplyAssignedVideoThumbnailsAsync(saved).ConfigureAwait(true);

            if (SelectedBrowserEntryItem is not null
                && string.Equals(SelectedBrowserEntryItem.FullPath, videoPath, StringComparison.OrdinalIgnoreCase))
            {
                SelectedBrowserEntryItem.Thumbnail = null;
                _thumbnailLoader.LoadItem(SelectedBrowserEntryItem, _thumbnailGeneration);
            }
        }, "Guardando miniaturas...").ConfigureAwait(true);
    }

    private async Task LoadAssignedVideoThumbnailsAsync(int mediaFileId, string videoPath)
    {
        try
        {
            if (!MediaFileExtensions.IsVideo(videoPath))
            {
                ClearAssignedVideoThumbnails();
                return;
            }

            var paths = await _mediaVaultService.GetThumbnailPathsAsync(mediaFileId).ConfigureAwait(true);
            FolderSessionPicturePicker.RegisterDedicatedPictures(videoPath, paths);
            await ApplyAssignedVideoThumbnailsAsync(paths).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ClearAssignedVideoThumbnails();
            ErrorMessage = $"No se pudieron cargar miniaturas: {ex.Message}";
        }
    }

    private async Task ApplyAssignedVideoThumbnailsAsync(IReadOnlyList<string> paths)
    {
        AssignedVideoThumbnails.Clear();
        foreach (var path in paths)
        {
            var item = new VideoThumbnailListItem { ImagePath = path };
            AssignedVideoThumbnails.Add(item);
            item.Preview = await Task.Run(() => LocalImageLoader.TryLoad(path, 72)).ConfigureAwait(true);
        }

        OnPropertyChanged(nameof(HasAssignedVideoThumbnails));
        OnPropertyChanged(nameof(VideoThumbnailHint));
        ClearVideoThumbnailsCommand.NotifyCanExecuteChanged();
    }

    private void ClearAssignedVideoThumbnails()
    {
        AssignedVideoThumbnails.Clear();
        OnPropertyChanged(nameof(HasAssignedVideoThumbnails));
        OnPropertyChanged(nameof(VideoThumbnailHint));
        ClearVideoThumbnailsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveFolderIconAsync()
    {
        if (SelectedBrowserEntry is not { IsDirectory: true })
            return;

        if (string.IsNullOrWhiteSpace(SelectedFolderIconPath))
        {
            ErrorMessage = "Seleccione una imagen para el icono de la carpeta.";
            return;
        }

        var folderPath = SelectedBrowserEntry.FullPath;
        var currentDirectory = CurrentDirectoryPath;

        await ExecuteBusyAsync(async () =>
        {
            await _appSettingsService.SaveFolderIconAsync(folderPath, SelectedFolderIconPath).ConfigureAwait(true);
            WindowsShellThumbnailProvider.ClearCache();
            await BrowseDirectoryAsync(currentDirectory, reselectEntryPath: folderPath).ConfigureAwait(true);
            NotifyFolderIconStateChanged();
        }, "Guardando miniatura...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanClearFolderIcon))]
    private async Task ClearFolderIconAsync()
    {
        if (SelectedBrowserEntry is not { IsDirectory: true })
            return;

        var folderPath = SelectedBrowserEntry.FullPath;
        var currentDirectory = CurrentDirectoryPath;

        await ExecuteBusyAsync(async () =>
        {
            await _appSettingsService.SaveFolderIconAsync(folderPath, iconPath: null).ConfigureAwait(true);
            SelectedFolderIconPath = null;

            foreach (var item in BrowserEntries.Where(entry =>
                         entry.IsDirectory
                         && string.Equals(entry.FullPath, folderPath, StringComparison.OrdinalIgnoreCase)))
            {
                item.Thumbnail = null;
            }

            if (SelectedBrowserEntryItem is not null)
                SelectedBrowserEntryItem.Thumbnail = null;

            WindowsShellThumbnailProvider.ClearCache();
            await BrowseDirectoryAsync(currentDirectory, reselectEntryPath: folderPath).ConfigureAwait(true);
            NotifyFolderIconStateChanged();
        }, "Limpiando miniatura...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedFile))]
    private async Task MoveAsync()
    {
        if (SelectedBrowserEntryItem is not { IsDirectory: false })
            return;

        if (string.IsNullOrWhiteSpace(IndexRootPath) || !Directory.Exists(IndexRootPath))
        {
            ErrorMessage = "Configure una carpeta raíz válida en Configuración.";
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta destino",
            InitialDirectory = CurrentDirectoryPath is { Length: > 0 } && Directory.Exists(CurrentDirectoryPath)
                ? CurrentDirectoryPath
                : IndexRootPath,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return;

        var sourcePath = SelectedBrowserEntryItem.FullPath;
        var destinationDirectory = dialog.FolderName;
        var fileName = SelectedBrowserEntryItem.Name;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar movimiento",
                $"¿Mover \"{fileName}\" a:\n{destinationDirectory}?",
                AppDialogKind.Information))
            return;

        await ExecuteBusyAsync(async () =>
        {
            var moved = await _mediaVaultService.MoveFileAsync(
                sourcePath,
                destinationDirectory,
                IndexRootPath).ConfigureAwait(true);

            WindowsShellThumbnailProvider.ClearCache();

            if (moved is not null)
            {
                await BrowseDirectoryAsync(
                    destinationDirectory,
                    reselectMediaFileId: moved.Id).ConfigureAwait(true);
            }
            else
            {
                await BrowseDirectoryAsync(
                    destinationDirectory,
                    reselectEntryPath: Path.Combine(Path.GetFullPath(destinationDirectory), fileName))
                    .ConfigureAwait(true);
            }
        }, "Moviendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RenameAsync()
    {
        if (SelectedMediaFile is null || string.IsNullOrWhiteSpace(NewFileName))
            return;

        var fileId = SelectedMediaFile.Id;
        var currentDirectory = CurrentDirectoryPath;

        await ExecuteBusyAsync(async () =>
        {
            await _mediaVaultService.RenameFileAsync(fileId, NewFileName).ConfigureAwait(true);
            await BrowseDirectoryAsync(currentDirectory, reselectMediaFileId: fileId).ConfigureAwait(true);
        }, "Renombrando archivo...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanCreateFolder))]
    private async Task CreateFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentDirectoryPath) || string.IsNullOrWhiteSpace(NewFolderName))
            return;

        var parentPath = CurrentDirectoryPath;
        var folderName = NewFolderName.Trim();
        var createdPath = Path.Combine(parentPath, folderName);

        await ExecuteBusyAsync(async () =>
        {
            await _mediaVaultService.CreateDirectoryAsync(parentPath, folderName).ConfigureAwait(true);
            NewFolderName = string.Empty;
            WindowsShellThumbnailProvider.ClearCache();
            await BrowseDirectoryAsync(parentPath, reselectEntryPath: createdPath).ConfigureAwait(true);
        }, "Creando carpeta...").ConfigureAwait(true);
    }

    private async Task LoadSelectedVideoResolutionAsync(string? path)
    {
        SelectedVideoResolutionLabel = null;

        if (string.IsNullOrWhiteSpace(path) || !MediaFileExtensions.IsVideo(path))
            return;

        var expectedPath = path;
        var label = await VideoResolutionProbe.TryGetResolutionLabelAsync(path).ConfigureAwait(true);

        if (!string.Equals(SelectedBrowserEntryItem?.FullPath, expectedPath, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(SelectedMediaFile?.Path, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectedVideoResolutionLabel = label;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedEntry))]
    private async Task DeleteAsync()
    {
        if (SelectedBrowserEntryItem is null)
            return;

        var entryName = SelectedBrowserEntryItem.Name;
        var isDirectory = SelectedBrowserEntryItem.IsDirectory;
        var confirmMessage = isDirectory
            ? $"¿Eliminar permanentemente la carpeta \"{entryName}\" y todo su contenido?\n\nEsta acción no se puede deshacer."
            : $"¿Eliminar permanentemente \"{entryName}\"?\n\nEsta acción no se puede deshacer.";

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                confirmMessage,
                AppDialogKind.Warning))
            return;

        var currentDirectory = CurrentDirectoryPath;
        var entryPath = SelectedBrowserEntryItem.FullPath;

        await ExecuteBusyAsync(async () =>
        {
            if (isDirectory)
            {
                if (string.IsNullOrWhiteSpace(IndexRootPath))
                    throw new InvalidOperationException("Configure una carpeta raíz válida en Configuración.");

                await _mediaVaultService
                    .DeleteDirectoryAsync(entryPath, IndexRootPath)
                    .ConfigureAwait(true);
            }
            else if (SelectedBrowserEntryItem.MediaFile is not null)
            {
                await _mediaVaultService.DeleteFileAsync(SelectedBrowserEntryItem.MediaFile.Id).ConfigureAwait(true);
            }
            else
            {
                var indexed = await _mediaVaultService.GetByPathAsync(entryPath).ConfigureAwait(true);
                if (indexed is not null)
                {
                    await _mediaVaultService.DeleteFileAsync(indexed.Id).ConfigureAwait(true);
                }
                else
                {
                    if (!File.Exists(entryPath))
                        throw new FileNotFoundException("El archivo no existe.");

                    File.Delete(entryPath);
                }
            }

            WindowsShellThumbnailProvider.ClearCache();
            await BrowseDirectoryAsync(currentDirectory).ConfigureAwait(true);
        }, isDirectory ? "Eliminando carpeta..." : "Eliminando archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveRankingsAsync()
    {
        if (SelectedMediaFile is null)
            return;

        try
        {
            ErrorMessage = null;
            var updated = await _mediaVaultService.UpdateRankingsAsync(
                SelectedMediaFile.Id,
                MediaFileRankingScale.ToStorage(RankingCalidad),
                MediaFileRankingScale.ToStorage(RankingContenido),
                MediaFileRankingScale.ToStorage(RankingGusto)).ConfigureAwait(true);

            ApplyUpdatedMediaFileLocally(updated);
            OnPropertyChanged(nameof(RankingGlobal));
            OnPropertyChanged(nameof(RankingGlobalStars));
            OnPropertyChanged(nameof(HasFileRanking));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (SelectedMediaFile is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(SelectedMediaFile.Id).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo.");

            ApplyUpdatedMediaFileLocally(opened);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenWithVlcAsync()
    {
        if (SelectedMediaFile is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(SelectedMediaFile.Id, preferVlc: true).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            ApplyUpdatedMediaFileLocally(opened);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    private async Task SaveFileCategoriesAsync()
    {
        if (SelectedMediaFile is null || !CanAssignVideoCategory)
            return;

        var categoryIds = FileCategorySelections
            .Where(item => item.IsSelected)
            .Select(item => item.CategoryId)
            .ToList();

        try
        {
            ErrorMessage = null;
            var updated = await _mediaVaultService.UpdateCategoriesAsync(SelectedMediaFile.Id, categoryIds)
                .ConfigureAwait(true);
            ApplyUpdatedMediaFileLocally(updated);
            SyncFileCategorySelections(updated);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task SaveFileActressesAsync()
    {
        if (SelectedMediaFile is null || !CanAssignActress)
            return;

        var actressIds = FileActressSelections
            .Where(item => item.IsSelected)
            .Select(item => item.ActressId)
            .ToList();

        try
        {
            ErrorMessage = null;
            var updated = await _mediaVaultService.UpdateActressesAsync(SelectedMediaFile.Id, actressIds)
                .ConfigureAwait(true);
            ApplyUpdatedMediaFileLocally(updated);
            SyncFileActressSelections(updated);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private async Task SaveFileProducersAsync()
    {
        if (SelectedMediaFile is null || !CanAssignProducer)
            return;

        var producerIds = FileProducerSelections
            .Where(item => item.IsSelected)
            .Select(item => item.ProducerId)
            .ToList();

        try
        {
            ErrorMessage = null;
            var updated = await _mediaVaultService.UpdateProducersAsync(SelectedMediaFile.Id, producerIds)
                .ConfigureAwait(true);
            ApplyUpdatedMediaFileLocally(updated);
            SyncFileProducerSelections(updated);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

public sealed record BrowserSortOption(string Label, MediaVaultBrowserSortField Field);

public sealed record BrowserSortDirectionOption(string Label, bool IsAscending);

public sealed record BrowserMediaTypeFilterOption(string Label, BrowserMediaTypeFilter Kind);

public enum BrowserMediaTypeFilter
{
    All,
    Directories,
    Images,
    Videos
}
