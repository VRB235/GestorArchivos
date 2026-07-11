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

using Microsoft.Win32;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class MediaVaultViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IMediaVaultService _mediaVaultService;
    private readonly IVideoCategoryService _videoCategoryService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IAppDialogService _appDialogService;
    private readonly BrowserThumbnailLoader _thumbnailLoader;
    private List<MediaVaultBrowserEntry> _directoryEntries = [];
    private int _thumbnailGeneration;
    private bool _suppressRankingPersistence;
    private bool _suppressCategoryPersistence;
    private bool _suppressShowHiddenPersistence;

    public MediaVaultViewModel(
        IMediaVaultService mediaVaultService,
        IVideoCategoryService videoCategoryService,
        IAppSettingsService appSettingsService,
        IAppDialogService appDialogService,
        BrowserThumbnailLoader thumbnailLoader)
    {
        _mediaVaultService = mediaVaultService;
        _videoCategoryService = videoCategoryService;
        _appSettingsService = appSettingsService;
        _appDialogService = appDialogService;
        _thumbnailLoader = thumbnailLoader;

        SortOptions =
        [
            new BrowserSortOption("Nombre", MediaVaultBrowserSortField.Name),
            new BrowserSortOption("Fecha de creación", MediaVaultBrowserSortField.Created),
            new BrowserSortOption("Fecha de actualización", MediaVaultBrowserSortField.Modified),
            new BrowserSortOption("Tipo de archivo", MediaVaultBrowserSortField.FileType)
        ];

        SortDirections =
        [
            new BrowserSortDirectionOption("Ascendente", true),
            new BrowserSortDirectionOption("Descendente", false)
        ];

        SelectedSortDirection = SortDirections[0];
        SelectedSortOption = SortOptions[0];
    }

    public ObservableCollection<CategoryFilterTagItem> CategoryFilterTags { get; } = [];

    public ObservableCollection<FileCategorySelectionItem> FileCategorySelections { get; } = [];

    public string Title => "File & Media Vault";

    public string Subtitle => "Indexación y gestión de archivos multimedia locales";

    public ObservableCollection<MediaVaultBrowserEntryItem> BrowserEntries { get; } = [];

    public ObservableCollection<BrowserSortOption> SortOptions { get; }

    public ObservableCollection<BrowserSortDirectionOption> SortDirections { get; }

    [ObservableProperty]
    private BrowserSortOption? _selectedSortOption;

    [ObservableProperty]
    private BrowserSortDirectionOption? _selectedSortDirection;

    [ObservableProperty]
    private bool _showHiddenFilesAndFolders;

    [ObservableProperty]
    private MediaVaultBrowserEntryItem? _selectedBrowserEntryItem;

    [ObservableProperty]
    private MediaFile? _selectedMediaFile;

    [ObservableProperty]
    private string _indexRootPath = string.Empty;

    [ObservableProperty]
    private string _currentDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _newFileName = string.Empty;

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
    private string? _lastIndexSummary;

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

    public bool CanAssignVideoCategory => CanEditSelectedFile;

    public string FileCategoryHint =>
        "Pulse un tag para asignar o quitar. Gestione la lista en el módulo «Categorías».";

    public bool CanDeleteSelectedFile => SelectedBrowserEntryItem is { IsDirectory: false };

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

    public async Task InitializeAsync()
    {
        await LoadVideoCategoriesAsync().ConfigureAwait(true);
        await LoadIndexRootPathAsync().ConfigureAwait(true);
        await BrowseDirectoryAsync(IndexRootPath).ConfigureAwait(true);
    }

    private async Task LoadVideoCategoriesAsync()
    {
        var categories = await _videoCategoryService.GetAllAsync().ConfigureAwait(true);
        RebuildCategoryFilterTags(categories);
        RebuildFileCategorySelections(categories, SelectedMediaFile);
    }

    private void RebuildCategoryFilterTags(IReadOnlyList<VideoCategory> categories)
    {
        var previouslySelected = CategoryFilterTags
            .Where(tag => tag.IsSelected)
            .Select(tag => tag.CategoryId)
            .ToHashSet();

        CategoryFilterTags.Clear();

        var uncategorizedTag = new CategoryFilterTagItem
        {
            CategoryId = VideoCategoryFilterOption.UncategorizedSentinel,
            Name = "Sin categoría",
            IsSelected = previouslySelected.Contains(VideoCategoryFilterOption.UncategorizedSentinel)
        };
        uncategorizedTag.SelectionChanged = OnCategoryFilterChanged;
        CategoryFilterTags.Add(uncategorizedTag);

        foreach (var category in categories)
        {
            var tag = new CategoryFilterTagItem
            {
                CategoryId = category.Id,
                Name = category.Name,
                IsSelected = previouslySelected.Contains(category.Id)
            };
            tag.SelectionChanged = OnCategoryFilterChanged;
            CategoryFilterTags.Add(tag);
        }
    }

    private void OnCategoryFilterChanged() =>
        ApplySortToBrowser();

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

    partial void OnSelectedSortOptionChanged(BrowserSortOption? value) =>
        ApplySortToBrowser();

    partial void OnSelectedSortDirectionChanged(BrowserSortDirectionOption? value) =>
        ApplySortToBrowser();

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
            FilterEntriesByCategory(_directoryEntries, CategoryFilterTags),
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
    }

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
            _ => ascending
                ? query.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                : query.OrderByDescending(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
        };

        return query.ToList();
    }

    private static IEnumerable<MediaVaultBrowserEntry> FilterEntriesByCategory(
        IEnumerable<MediaVaultBrowserEntry> entries,
        IReadOnlyList<CategoryFilterTagItem> filterTags)
    {
        var selectedCategoryIds = filterTags
            .Where(tag => tag.IsSelected && tag.CategoryId > VideoCategoryFilterOption.UncategorizedSentinel)
            .Select(tag => tag.CategoryId)
            .ToHashSet();

        var includeUncategorized = filterTags.Any(tag =>
            tag.IsSelected && tag.CategoryId == VideoCategoryFilterOption.UncategorizedSentinel);

        if (selectedCategoryIds.Count == 0 && !includeUncategorized)
            return entries;

        return entries.Where(entry =>
        {
            if (entry.IsDirectory)
                return false;

            if (entry.MediaFile is null)
                return includeUncategorized;

            if (entry.MediaFile.Categories.Count == 0)
                return includeUncategorized;

            if (selectedCategoryIds.Count == 0)
                return false;

            return entry.MediaFile.Categories.Any(category => selectedCategoryIds.Contains(category.Id));
        });
    }

    private void NotifyFileCategoryStateChanged()
    {
        OnPropertyChanged(nameof(CanAssignVideoCategory));
        OnPropertyChanged(nameof(ShowFileCategorySection));
        OnPropertyChanged(nameof(FileCategoryHint));
    }

    private void NotifyNavigationChanged() =>
        OnPropertyChanged(nameof(CanNavigateUp));

    partial void OnSelectedBrowserEntryItemChanged(MediaVaultBrowserEntryItem? value)
    {
        OnPropertyChanged(nameof(SelectedBrowserEntry));
        OnPropertyChanged(nameof(IsSelectedFolder));
        OnPropertyChanged(nameof(IsSelectedFile));

        OnPropertyChanged(nameof(CanDeleteSelectedFile));
        DeleteCommand.NotifyCanExecuteChanged();
        NotifyFolderIconStateChanged();

        if (value is null)
        {
            SelectedMediaFile = null;
            SelectedFolderIconPath = null;
            NewFileName = string.Empty;
            RankingCalidad = 0;
            RankingContenido = 0;
            RankingGusto = 0;
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
                RankingCalidad = 0;
                RankingContenido = 0;
                RankingGusto = 0;
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
                OnPropertyChanged(nameof(CanEditSelectedFile));
                NotifyFileCategoryStateChanged();
                return;
            }

            NewFileName = Path.GetFileNameWithoutExtension(value.MediaFile.Name);
            RankingCalidad = MediaFileRankingScale.ToStars(value.MediaFile.RankingCalidad);
            RankingContenido = MediaFileRankingScale.ToStars(value.MediaFile.RankingContenido);
            RankingGusto = MediaFileRankingScale.ToStars(value.MediaFile.RankingGusto);
            SyncFileCategorySelections(value.MediaFile);

            OnPropertyChanged(nameof(CanEditSelectedFile));
            NotifyFileCategoryStateChanged();
        }
        finally
        {
            _suppressRankingPersistence = false;
        }
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
        if (item.MediaFile is not null)
        {
            var fileId = item.MediaFile.Id;
            var currentDirectory = CurrentDirectoryPath;

            try
            {
                ErrorMessage = null;
                var opened = await _mediaVaultService.OpenFileAsync(fileId).ConfigureAwait(true);
                if (!opened)
                {
                    ErrorMessage = "No se pudo abrir el archivo.";
                    return;
                }

                await BrowseDirectoryAsync(currentDirectory, reselectMediaFileId: fileId).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }

            return;
        }

        if (!File.Exists(item.FullPath))
        {
            ErrorMessage = "El archivo no existe.";
            return;
        }

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

    [RelayCommand]
    private async Task IndexAsync()
    {
        await LoadIndexRootPathAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(IndexRootPath))
        {
            ErrorMessage = "Configure la carpeta de indexación en el módulo Configuración.";
            return;
        }

        if (!Directory.Exists(IndexRootPath))
        {
            ErrorMessage = "La carpeta configurada no existe. Actualícela en Configuración.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var result = await _mediaVaultService.IndexDirectoryAsync(IndexRootPath).ConfigureAwait(true);
            LastIndexSummary =
                $"Indexados: {result.FilesIndexed} | Nuevos: {result.FilesAdded} | Actualizados: {result.FilesUpdated} | Omitidos: {result.FilesSkipped}";
            await BrowseDirectoryAsync(IndexRootPath).ConfigureAwait(true);
        }, "Indexando archivos...").ConfigureAwait(true);
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

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedFile))]
    private async Task DeleteAsync()
    {
        if (SelectedBrowserEntryItem is not { IsDirectory: false })
            return;

        var fileName = SelectedBrowserEntryItem.Name;
        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar permanentemente \"{fileName}\"?\n\nEsta acción no se puede deshacer.",
                AppDialogKind.Warning))
            return;

        var currentDirectory = CurrentDirectoryPath;
        var filePath = SelectedBrowserEntryItem.FullPath;

        await ExecuteBusyAsync(async () =>
        {
            if (SelectedBrowserEntryItem.MediaFile is not null)
            {
                await _mediaVaultService.DeleteFileAsync(SelectedBrowserEntryItem.MediaFile.Id).ConfigureAwait(true);
            }
            else
            {
                var indexed = await _mediaVaultService.GetByPathAsync(filePath).ConfigureAwait(true);
                if (indexed is not null)
                {
                    await _mediaVaultService.DeleteFileAsync(indexed.Id).ConfigureAwait(true);
                }
                else
                {
                    if (!File.Exists(filePath))
                        throw new FileNotFoundException("El archivo no existe.");

                    File.Delete(filePath);
                }
            }

            WindowsShellThumbnailProvider.ClearCache();
            await BrowseDirectoryAsync(currentDirectory).ConfigureAwait(true);
        }, "Eliminando archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveRankingsAsync()
    {
        if (SelectedMediaFile is null)
            return;

        var fileId = SelectedMediaFile.Id;
        var currentDirectory = CurrentDirectoryPath;

        try
        {
            ErrorMessage = null;
            await _mediaVaultService.UpdateRankingsAsync(
                fileId,
                MediaFileRankingScale.ToStorage(RankingCalidad),
                MediaFileRankingScale.ToStorage(RankingContenido),
                MediaFileRankingScale.ToStorage(RankingGusto)).ConfigureAwait(true);
            await BrowseDirectoryAsync(currentDirectory, reselectMediaFileId: fileId).ConfigureAwait(true);
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

        var fileId = SelectedMediaFile.Id;
        var currentDirectory = CurrentDirectoryPath;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(fileId).ConfigureAwait(true);
            if (!opened)
                throw new InvalidOperationException("No se pudo abrir el archivo.");

            await BrowseDirectoryAsync(currentDirectory, reselectMediaFileId: fileId).ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenWithVlcAsync()
    {
        if (SelectedMediaFile is null)
            return;

        var fileId = SelectedMediaFile.Id;
        var currentDirectory = CurrentDirectoryPath;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(fileId, preferVlc: true).ConfigureAwait(true);
            if (!opened)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            await BrowseDirectoryAsync(currentDirectory, reselectMediaFileId: fileId).ConfigureAwait(true);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    private async Task SaveFileCategoriesAsync()
    {
        if (SelectedMediaFile is null || !CanAssignVideoCategory)
            return;

        var fileId = SelectedMediaFile.Id;
        var currentDirectory = CurrentDirectoryPath;
        var categoryIds = FileCategorySelections
            .Where(item => item.IsSelected)
            .Select(item => item.CategoryId)
            .ToList();

        try
        {
            ErrorMessage = null;
            await _mediaVaultService.UpdateCategoriesAsync(fileId, categoryIds).ConfigureAwait(true);
            await BrowseDirectoryAsync(currentDirectory, reselectMediaFileId: fileId).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

public sealed record BrowserSortOption(string Label, MediaVaultBrowserSortField Field);

public sealed record BrowserSortDirectionOption(string Label, bool IsAscending);
