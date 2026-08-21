using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Charts;
using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.Dashboard;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IMediaVaultService _mediaVaultService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IRankedVideoRecommendationSession _rankedSession;
    private int _recommendationThumbGeneration;
    private int _rankedRecommendationThumbGeneration;

    public DashboardViewModel(
        IDashboardService dashboardService,
        IMediaVaultService mediaVaultService,
        IAppSettingsService appSettingsService,
        IRankedVideoRecommendationSession rankedSession)
    {
        _dashboardService = dashboardService;
        _mediaVaultService = mediaVaultService;
        _appSettingsService = appSettingsService;
        _rankedSession = rankedSession;
    }

    public string Title => "Dashboard & Estadísticas";

    public string Subtitle => "Tops de videos, ranking y recomendación";

    public ObservableCollection<DashboardChartPanelViewModel> PrimaryChartSections { get; } = [];

    public ObservableCollection<DashboardChartPanelViewModel> SecondaryChartSections { get; } = [];

    public ObservableCollection<DashboardRecommendationItem> RecommendedVideos { get; } = [];

    public ObservableCollection<DashboardRecommendationItem> RankedRecommendedVideos { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AverageGlobalRankingStars))]
    private double _averageGlobalRanking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AverageVideoRankingStars))]
    private double _averageVideoRanking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AveragePhotoRankingStars))]
    private double _averagePhotoRanking;

    public int AverageGlobalRankingStars =>
        MediaFileRankingScale.ToDisplayStars(AverageGlobalRanking);

    public int AverageVideoRankingStars =>
        MediaFileRankingScale.ToDisplayStars(AverageVideoRanking);

    public int AveragePhotoRankingStars =>
        MediaFileRankingScale.ToDisplayStars(AveragePhotoRanking);

    [ObservableProperty]
    private int _totalMediaFiles;

    [ObservableProperty]
    private int _totalVideos;

    [ObservableProperty]
    private int _totalPhotos;

    [ObservableProperty]
    private int _totalWebLinks;

    [ObservableProperty]
    private int _totalQuickNotes;

    [ObservableProperty]
    private int _totalVideoOpens;

    [ObservableProperty]
    private int _videosNeverOpened;

    [ObservableProperty]
    private int _videosUnrated;

    public bool HasRecommendation => RecommendedVideos.Count > 0;

    public bool HasRankedRecommendation => RankedRecommendedVideos.Count > 0;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(async () =>
        {
            await ReloadAsync(preserveRecommendation: false).ConfigureAwait(true);
        }, "Actualizando estadísticas...");

    private async Task ReloadAsync(bool preserveRecommendation)
    {
        var keepMixed = preserveRecommendation && RecommendedVideos.Count > 0;
        var keepRanked = preserveRecommendation && RankedRecommendedVideos.Count > 0;
        var stats = await _dashboardService.GetStatisticsAsync().ConfigureAwait(true);

        AverageGlobalRanking = stats.AverageGlobalRanking;
        AverageVideoRanking = stats.AverageVideoRanking;
        AveragePhotoRanking = stats.AveragePhotoRanking;
        TotalMediaFiles = stats.TotalMediaFiles;
        TotalVideos = stats.TotalVideos;
        TotalPhotos = stats.TotalPhotos;
        TotalWebLinks = stats.TotalWebLinks;
        TotalQuickNotes = stats.TotalQuickNotes;
        TotalVideoOpens = stats.TotalVideoOpens;
        VideosNeverOpened = stats.VideosNeverOpened;
        VideosUnrated = stats.VideosUnrated;

        BuildChartSections(stats);

        // Ranking primero (pool más restringido); luego mixtas excluyen esos IDs.
        if (keepRanked)
            await RefreshPreservedRecommendationsAsync(RankedRecommendedVideos, showTierLabel: true).ConfigureAwait(true);
        else
            await LoadRankedRecommendationAsync(advance: false).ConfigureAwait(true);

        if (keepMixed)
            await RefreshPreservedRecommendationsAsync(RecommendedVideos, showTierLabel: false).ConfigureAwait(true);
        else
            await LoadRecommendationAsync(excludeCurrent: false).ConfigureAwait(true);
    }

    private async Task RefreshPreservedRecommendationsAsync(
        ObservableCollection<DashboardRecommendationItem> items,
        bool showTierLabel)
    {
        if (items.Count == 0)
            return;

        var refreshed = new List<DashboardRecommendationItem>(items.Count);
        foreach (var item in items.ToList())
        {
            var stats = await _dashboardService.GetVideoStatsByIdAsync(item.Video.Id).ConfigureAwait(true);
            if (stats is null)
                continue;

            refreshed.Add(new DashboardRecommendationItem(stats, showTierLabel)
            {
                Thumbnail = item.Thumbnail,
                ResolutionLabel = item.ResolutionLabel
            });
        }

        items.Clear();
        foreach (var item in refreshed)
            items.Add(item);

        OnPropertyChanged(showTierLabel ? nameof(HasRankedRecommendation) : nameof(HasRecommendation));
    }

    private void BuildChartSections(DashboardStatistics stats)
    {
        PrimaryChartSections.Clear();
        SecondaryChartSections.Clear();

        AddBarChart(
            PrimaryChartSections,
            "Top 10 videos más vistos",
            "Sin videos abiertos desde la app. Abra videos en Media Vault.",
            stats.Top10MostViewedVideos,
            isExpanded: true,
            panel => DashboardChartFactory.PopulateTopViewsChart(
                stats.Top10MostViewedVideos,
                panel,
                onPointClicked: index => _ = OpenChartMediaFileAsync(panel, index)));

        AddBarChart(
            PrimaryChartSections,
            "Top 10 videos mejor rankeados",
            "Sin videos calificados. Use estrellas en Media Vault.",
            stats.Top10BestRankedVideos,
            isExpanded: true,
            panel => DashboardChartFactory.PopulateTopRankedChart(
                stats.Top10BestRankedVideos,
                panel,
                onPointClicked: index => _ = OpenChartMediaFileAsync(panel, index)));

        AddBarChart(
            SecondaryChartSections,
            "Top 10 fotos más vistas",
            "Sin fotos abiertas desde la app. Abra imágenes en Media Vault.",
            stats.Top10MostViewedPhotos,
            isExpanded: false,
            panel => DashboardChartFactory.PopulateTopViewsChart(
                stats.Top10MostViewedPhotos,
                panel,
                SkiaSharp.SKColor.Parse("#10B981"),
                index => _ = OpenChartMediaFileAsync(panel, index)));

        AddBarChart(
            SecondaryChartSections,
            "Top 10 fotos mejor rankeadas",
            "Sin fotos calificadas. Use estrellas en Media Vault.",
            stats.Top10BestRankedPhotos,
            isExpanded: false,
            panel => DashboardChartFactory.PopulateTopRankedChart(
                stats.Top10BestRankedPhotos,
                panel,
                SkiaSharp.SKColor.Parse("#F59E0B"),
                index => _ = OpenChartMediaFileAsync(panel, index)));

        AddPieChart(
            SecondaryChartSections,
            "Archivos por categoría",
            "Sin archivos indexados o sin categorías asignadas.",
            stats.VideoDistributionByCategory.Any(item => item.Count > 0),
            isExpanded: false,
            panel => DashboardChartFactory.PopulateMediaDistributionPieChart(
                stats.VideoDistributionByCategory,
                panel.Series));

        AddColumnChart(
            SecondaryChartSections,
            "Ranking promedio por categoría",
            "Califique archivos con categoría para ver este gráfico.",
            stats.AverageRankingByVideoCategory.Count > 0,
            isExpanded: false,
            panel =>
            {
                DashboardChartFactory.PopulateCategoryRankingChart(
                    stats.AverageRankingByVideoCategory,
                    panel.Series,
                    out var xAxes,
                    out var yAxes);
                panel.XAxes = xAxes;
                panel.YAxes = yAxes;
            });

        AddPieChart(
            SecondaryChartSections,
            "Enlaces por categoría",
            "Sin enlaces registrados. Agregue enlaces en Link Manager.",
            stats.LinkDistributionByCategory.Any(item => item.Count > 0),
            isExpanded: false,
            panel => DashboardChartFactory.PopulateCategoryPieChart(
                stats.LinkDistributionByCategory,
                panel.Series));
    }

    [RelayCommand]
    private Task RefreshRecommendationAsync() =>
        RunBusyCoreAsync(
            () => LoadRecommendationAsync(excludeCurrent: true),
            "Eligiendo otras recomendaciones...");

    [RelayCommand]
    private Task RefreshRankedRecommendationAsync() =>
        RunBusyCoreAsync(
            () => LoadRankedRecommendationAsync(advance: true),
            "Eligiendo otros rankeados...");

    [RelayCommand]
    private async Task OpenRecommendedAsync(DashboardRecommendationItem? item)
    {
        if (item is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(item.Video.Id).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo. Verifique que exista en disco.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenRecommendedWithVlcAsync(DashboardRecommendationItem? item)
    {
        if (item is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService
                .OpenFileAsync(item.Video.Id, preferVlc: true)
                .ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenRankedRecommendedAsync(DashboardRecommendationItem? item)
    {
        if (item is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(item.Video.Id).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo. Verifique que exista en disco.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task OpenRankedRecommendedWithVlcAsync(DashboardRecommendationItem? item)
    {
        if (item is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService
                .OpenFileAsync(item.Video.Id, preferVlc: true)
                .ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    private async Task LoadRecommendationAsync(bool excludeCurrent)
    {
        const int count = VideoRecommendation.DefaultPickCount;
        var otherPanelIds = GetRankedRecommendedVideoIds();
        var selfIds = GetRecommendedVideoIds();

        var preferredExclude = new HashSet<int>(otherPanelIds);
        if (excludeCurrent)
            preferredExclude.UnionWith(selfIds);

        var recommendations = (await _dashboardService
            .GetVideoRecommendationsAsync(preferredExclude, count, reuseWhenExhausted: false)
            .ConfigureAwait(true)).ToList();

        // Si no hay suficientes sin solapar, completar permitiendo cruce con el otro panel.
        if (recommendations.Count < count)
        {
            var softExclude = new HashSet<int>(recommendations.Select(video => video.Id));
            if (excludeCurrent)
                softExclude.UnionWith(selfIds);

            var extra = await _dashboardService
                .GetVideoRecommendationsAsync(
                    softExclude,
                    count - recommendations.Count,
                    reuseWhenExhausted: true)
                .ConfigureAwait(true);

            foreach (var video in extra)
            {
                if (softExclude.Add(video.Id))
                    recommendations.Add(video);
            }
        }

        await SetRecommendationsAsync(recommendations).ConfigureAwait(true);
    }

    /// <param name="advance">
    /// false: restaurar el lote actual de sesión (solo si está completo) o elegir uno nuevo.
    /// true: pedir otro lote distinto (sin repetir los ya mostrados; al agotar, reinicia el ciclo).
    /// </param>
    private async Task LoadRankedRecommendationAsync(bool advance)
    {
        const int count = RankedVideoRecommendation.DefaultPickCount;
        var otherPanelIds = GetRecommendedVideoIds();

        if (!advance && _rankedSession.CurrentMediaFileIds.Count >= count)
        {
            var restored = new List<MediaFileViewStats>();
            foreach (var id in _rankedSession.CurrentMediaFileIds)
            {
                var stats = await _dashboardService.GetVideoStatsByIdAsync(id).ConfigureAwait(true);
                if (stats is null)
                    continue;

                restored.Add(stats);
            }

            // Restaurar solo si el lote está completo y no solapa con «para ver».
            if (restored.Count >= count
                && restored.All(video => !otherPanelIds.Contains(video.Id)))
            {
                await SetRankedRecommendationsAsync(restored).ConfigureAwait(true);
                return;
            }
        }

        if (!advance && _rankedSession.CurrentMediaFileIds.Count is > 0 and < count)
            _rankedSession.Reset();

        var preferredExclude = new HashSet<int>(otherPanelIds);
        if (advance)
            preferredExclude.UnionWith(_rankedSession.ShownMediaFileIds);

        var picked = (await _dashboardService
            .GetRankedVideoRecommendationsAsync(preferredExclude, count)
            .ConfigureAwait(true)).ToList();

        if ((picked.Count == 0 || (advance && picked.Count < count))
            && _rankedSession.ShownMediaFileIds.Count > 0)
        {
            _rankedSession.Reset();
            preferredExclude = new HashSet<int>(otherPanelIds);
            picked = (await _dashboardService
                .GetRankedVideoRecommendationsAsync(preferredExclude, count)
                .ConfigureAwait(true)).ToList();
        }

        // Completar lote si aún faltan (permite solape solo como último recurso).
        if (picked.Count < count)
        {
            var softExclude = picked.Select(video => video.Id).ToHashSet();
            if (advance)
                softExclude.UnionWith(_rankedSession.ShownMediaFileIds);

            var extra = await _dashboardService
                .GetRankedVideoRecommendationsAsync(softExclude, count - picked.Count)
                .ConfigureAwait(true);

            foreach (var video in extra)
            {
                if (softExclude.Add(video.Id))
                    picked.Add(video);
            }
        }

        if (picked.Count > 0)
            _rankedSession.SetCurrent(picked.Select(video => video.Id).ToArray());

        await SetRankedRecommendationsAsync(picked).ConfigureAwait(true);
    }

    private HashSet<int> GetRecommendedVideoIds() =>
        RecommendedVideos.Select(item => item.Video.Id).ToHashSet();

    private HashSet<int> GetRankedRecommendedVideoIds() =>
        RankedRecommendedVideos.Select(item => item.Video.Id).ToHashSet();

    private async Task SetRecommendationsAsync(IReadOnlyList<MediaFileViewStats> recommendations)
    {
        RecommendedVideos.Clear();
        OnPropertyChanged(nameof(HasRecommendation));

        if (recommendations.Count == 0)
            return;

        var generation = ++_recommendationThumbGeneration;
        var items = recommendations
            .Select(video => new DashboardRecommendationItem(video, showTierLabel: false))
            .ToList();

        foreach (var item in items)
            RecommendedVideos.Add(item);

        OnPropertyChanged(nameof(HasRecommendation));

        // Miniaturas/resolución en segundo plano: no bloquean el fin de IsBusy ni el arranque.
        _ = LoadRecommendationDetailsAsync(items, generation, isRanked: false);
    }

    private async Task SetRankedRecommendationsAsync(IReadOnlyList<MediaFileViewStats> recommendations)
    {
        RankedRecommendedVideos.Clear();
        OnPropertyChanged(nameof(HasRankedRecommendation));

        if (recommendations.Count == 0)
            return;

        var generation = ++_rankedRecommendationThumbGeneration;
        var items = recommendations
            .Select(video => new DashboardRecommendationItem(video, showTierLabel: true))
            .ToList();

        foreach (var item in items)
            RankedRecommendedVideos.Add(item);

        OnPropertyChanged(nameof(HasRankedRecommendation));

        _ = LoadRecommendationDetailsAsync(items, generation, isRanked: true);
    }

    private async Task LoadRecommendationDetailsAsync(
        IReadOnlyList<DashboardRecommendationItem> items,
        int generation,
        bool isRanked)
    {
        try
        {
            await VideoThumbnailSessionBootstrap
                .PrefetchWithDedicatedAsync(
                    _mediaVaultService,
                    items.Select(item =>
                    {
                        var folder = Path.GetDirectoryName(item.Video.Path) ?? string.Empty;
                        return (ItemKey: item.Video.Path, FolderPath: folder);
                    }))
                .ConfigureAwait(true);

            foreach (var item in items)
            {
                if (isRanked
                        ? generation != _rankedRecommendationThumbGeneration
                        : generation != _recommendationThumbGeneration)
                    return;

                item.ResolutionLabel = await VideoResolutionProbe
                    .TryGetResolutionLabelAsync(item.Video.Path)
                    .ConfigureAwait(true);

                item.Thumbnail = await LoadVideoActressThumbnailAsync(item.Video.Path, 160)
                    .ConfigureAwait(true);
            }
        }
        catch
        {
            // Detalles cosméticos: un fallo de I/O no debe tumbar el Dashboard.
        }
    }

    /// <summary>
    /// Prioridad: miniaturas asignadas / Pictures (por video) → icono de carpeta → shell de carpeta.
    /// </summary>
    private async Task<ImageSource?> LoadVideoActressThumbnailAsync(string videoPath, int size)
    {
        var folderPath = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        try
        {
            folderPath = Path.GetFullPath(folderPath);
        }
        catch (Exception)
        {
            return null;
        }

        if (!MediaPathEligibility.IsUsableMediaPath(folderPath))
            return null;

        var sessionPicture = await Task.Run(() =>
            FolderSessionPicturePicker.TryLoadThumbnailForItem(folderPath, videoPath, size))
            .ConfigureAwait(true);
        if (sessionPicture is not null)
            return sessionPicture;

        var iconPath = await _appSettingsService.GetFolderIconPathAsync(folderPath).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            return await Task.Run(() => LocalImageLoader.TryLoad(iconPath, size)).ConfigureAwait(true);
        }

        return await WindowsShellThumbnailProvider
            .GetThumbnailAsync(folderPath, isDirectory: true, size)
            .ConfigureAwait(true);
    }

    private void AddBarChart(
        ObservableCollection<DashboardChartPanelViewModel> target,
        string title,
        string emptyMessage,
        IReadOnlyList<MediaFileViewStats> data,
        bool isExpanded,
        Action<DashboardChartPanelViewModel> populate)
    {
        var panel = CreatePanel(title, emptyMessage, isPie: false, data.Count > 0, isExpanded);
        panel.MediaFiles = data.ToArray();
        populate(panel);
        target.Add(panel);
        _ = PrefetchChartMediaThumbnailsAsync(data);
    }

    private async Task PrefetchChartMediaThumbnailsAsync(IReadOnlyList<MediaFileViewStats> data)
    {
        try
        {
            await VideoThumbnailSessionBootstrap
                .PrefetchWithDedicatedAsync(
                    _mediaVaultService,
                    data.Select(file =>
                    {
                        var folder = Path.GetDirectoryName(file.Path) ?? string.Empty;
                        return (ItemKey: file.Path, FolderPath: folder);
                    }))
                .ConfigureAwait(false);
        }
        catch
        {
            // Prefetch cosmético para hover de gráficos.
        }
    }

    private async Task OpenChartMediaFileAsync(DashboardChartPanelViewModel panel, int index)
    {
        if (index < 0 || index >= panel.MediaFiles.Length)
            return;

        var fileId = panel.MediaFiles[index].Id;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(fileId).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo. Verifique que exista en disco.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    private void AddColumnChart(
        ObservableCollection<DashboardChartPanelViewModel> target,
        string title,
        string emptyMessage,
        bool hasData,
        bool isExpanded,
        Action<DashboardChartPanelViewModel> populate)
    {
        var panel = CreatePanel(title, emptyMessage, isPie: false, hasData, isExpanded);
        populate(panel);
        target.Add(panel);
    }

    private void AddPieChart(
        ObservableCollection<DashboardChartPanelViewModel> target,
        string title,
        string emptyMessage,
        bool hasData,
        bool isExpanded,
        Action<DashboardChartPanelViewModel> populate)
    {
        var panel = CreatePanel(title, emptyMessage, isPie: true, hasData, isExpanded);
        populate(panel);
        target.Add(panel);
    }

    private static DashboardChartPanelViewModel CreatePanel(
        string title,
        string emptyMessage,
        bool isPie,
        bool hasData,
        bool isExpanded) =>
        new()
        {
            Title = title,
            EmptyMessage = emptyMessage,
            IsPie = isPie,
            HasData = hasData,
            IsExpanded = isExpanded,
            DrawMarginFrame = DashboardChartFactory.CreateDrawMarginFrame()
        };
}
