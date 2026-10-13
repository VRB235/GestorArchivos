using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    public string Subtitle => "Resumen, ranking y recomendación";

    public ObservableCollection<DashboardRecommendationItem> RecommendedVideos { get; } = [];

    public ObservableCollection<DashboardRecommendationItem> RankedRecommendedVideos { get; } = [];

    /// <summary>Ancho de tarjeta del carrusel: viewport / 5 para llenar la fila.</summary>
    [ObservableProperty]
    private double _recommendationCardWidth = 200;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AverageGlobalRankingStars))]
    [NotifyPropertyChangedFor(nameof(AverageGlobalRankingLabel))]
    private double _averageGlobalRanking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AverageVideoRankingStars))]
    [NotifyPropertyChangedFor(nameof(AverageVideoRankingLabel))]
    private double _averageVideoRanking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AveragePhotoRankingStars))]
    [NotifyPropertyChangedFor(nameof(AveragePhotoRankingLabel))]
    private double _averagePhotoRanking;

    public int AverageGlobalRankingStars =>
        MediaFileRankingScale.ToDisplayStars(AverageGlobalRanking);

    public int AverageVideoRankingStars =>
        MediaFileRankingScale.ToDisplayStars(AverageVideoRanking);

    public int AveragePhotoRankingStars =>
        MediaFileRankingScale.ToDisplayStars(AveragePhotoRanking);

    public string AverageGlobalRankingLabel => FormatRankingLabel(AverageGlobalRanking);

    public string AverageVideoRankingLabel => FormatRankingLabel(AverageVideoRanking);

    public string AveragePhotoRankingLabel => FormatRankingLabel(AveragePhotoRanking);

    [ObservableProperty]
    private int _totalMediaFiles;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideosOpened))]
    [NotifyPropertyChangedFor(nameof(VideosOpenedPercent))]
    [NotifyPropertyChangedFor(nameof(VideosOpenedSummary))]
    [NotifyPropertyChangedFor(nameof(VideosNeverOpenedSummary))]
    [NotifyPropertyChangedFor(nameof(VideosRated))]
    [NotifyPropertyChangedFor(nameof(VideosRatedPercent))]
    [NotifyPropertyChangedFor(nameof(VideosRatedSummary))]
    [NotifyPropertyChangedFor(nameof(VideosUnratedSummary))]
    [NotifyPropertyChangedFor(nameof(AverageOpensPerVideo))]
    [NotifyPropertyChangedFor(nameof(AverageOpensPerVideoLabel))]
    private int _totalVideos;

    [ObservableProperty]
    private int _totalPhotos;

    [ObservableProperty]
    private int _totalWebLinks;

    [ObservableProperty]
    private int _totalQuickNotes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AverageOpensPerVideo))]
    [NotifyPropertyChangedFor(nameof(AverageOpensPerVideoLabel))]
    private int _totalVideoOpens;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideosOpened))]
    [NotifyPropertyChangedFor(nameof(VideosOpenedPercent))]
    [NotifyPropertyChangedFor(nameof(VideosOpenedSummary))]
    [NotifyPropertyChangedFor(nameof(VideosNeverOpenedSummary))]
    private int _videosNeverOpened;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VideosRated))]
    [NotifyPropertyChangedFor(nameof(VideosRatedPercent))]
    [NotifyPropertyChangedFor(nameof(VideosRatedSummary))]
    [NotifyPropertyChangedFor(nameof(VideosUnratedSummary))]
    private int _videosUnrated;

    public int VideosOpened => Math.Max(0, TotalVideos - VideosNeverOpened);

    public int VideosRated => Math.Max(0, TotalVideos - VideosUnrated);

    public double VideosOpenedPercent => PercentOf(VideosOpened, TotalVideos);

    public double VideosRatedPercent => PercentOf(VideosRated, TotalVideos);

    public double AverageOpensPerVideo =>
        TotalVideos <= 0 ? 0 : (double)TotalVideoOpens / TotalVideos;

    public string VideosOpenedSummary =>
        TotalVideos <= 0
            ? "Sin videos indexados"
            : $"{VideosOpened} de {TotalVideos} ({VideosOpenedPercent:0}%)";

    public string VideosNeverOpenedSummary =>
        VideosNeverOpened == 1 ? "1 pendiente sin abrir" : $"{VideosNeverOpened} pendientes sin abrir";

    public string VideosRatedSummary =>
        TotalVideos <= 0
            ? "Sin videos indexados"
            : $"{VideosRated} de {TotalVideos} ({VideosRatedPercent:0}%)";

    public string VideosUnratedSummary =>
        VideosUnrated == 1 ? "1 sin calificar" : $"{VideosUnrated} sin calificar";

    public string AverageOpensPerVideoLabel =>
        TotalVideos <= 0
            ? "Sin videos indexados"
            : $"{AverageOpensPerVideo:0.#} aperturas por video";

    public bool HasRecommendation => RecommendedVideos.Count > 0;

    public bool HasRankedRecommendation => RankedRecommendedVideos.Count > 0;

    /// <summary>Quedan candidatos para ampliar el carrusel mixto (+5).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreRecommendationsCommand))]
    private bool _canLoadMoreRecommendations = true;

    /// <summary>Quedan candidatos para ampliar el carrusel por ranking (+5).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadMoreRankedRecommendationsCommand))]
    private bool _canLoadMoreRankedRecommendations = true;

    private static double PercentOf(int part, int total) =>
        total <= 0 ? 0 : 100.0 * part / total;

    private static string FormatRankingLabel(double average) =>
        average <= 0
            ? "Sin calificaciones"
            : $"{average:0.0} / {MediaFileRankingScale.MaxStars}";

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

    [RelayCommand]
    private Task RefreshRecommendationAsync() =>
        RunBusyCoreAsync(async () =>
        {
            CanLoadMoreRecommendations = true;
            await LoadRecommendationAsync(excludeCurrent: true).ConfigureAwait(true);
        }, "Eligiendo otras recomendaciones...");

    [RelayCommand]
    private Task RefreshRankedRecommendationAsync() =>
        RunBusyCoreAsync(async () =>
        {
            _rankedSession.Reset();
            CanLoadMoreRankedRecommendations = true;
            await LoadRankedRecommendationAsync(advance: false).ConfigureAwait(true);
        }, "Eligiendo otros rankeados...");

    [RelayCommand(CanExecute = nameof(CanLoadMoreRecommendations))]
    private Task LoadMoreRecommendationsAsync() =>
        RunBusyCoreAsync(
            () => AppendRecommendationsAsync(),
            "Cargando más recomendaciones...");

    [RelayCommand(CanExecute = nameof(CanLoadMoreRankedRecommendations))]
    private Task LoadMoreRankedRecommendationsAsync() =>
        RunBusyCoreAsync(
            () => AppendRankedRecommendationsAsync(),
            "Cargando más rankeados...");

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
        var recommendations = await PickMixedRecommendationsAsync(
                excludeCurrent: excludeCurrent,
                appendMode: false)
            .ConfigureAwait(true);

        await SetRecommendationsAsync(recommendations).ConfigureAwait(true);
        CanLoadMoreRecommendations = recommendations.Count > 0;
    }

    private async Task AppendRecommendationsAsync()
    {
        var batch = await PickMixedRecommendationsAsync(
                excludeCurrent: true,
                appendMode: true)
            .ConfigureAwait(true);

        if (batch.Count == 0)
        {
            CanLoadMoreRecommendations = false;
            return;
        }

        await AppendRecommendationItemsAsync(batch, isRanked: false).ConfigureAwait(true);
        CanLoadMoreRecommendations = batch.Count >= VideoRecommendation.DefaultPickCount;
    }

    private async Task<List<MediaFileViewStats>> PickMixedRecommendationsAsync(
        bool excludeCurrent,
        bool appendMode)
    {
        const int count = VideoRecommendation.DefaultPickCount;
        var otherPanelIds = GetRankedRecommendedVideoIds();
        var selfIds = GetRecommendedVideoIds();

        var preferredExclude = new HashSet<int>(otherPanelIds);
        if (excludeCurrent || appendMode)
            preferredExclude.UnionWith(selfIds);

        var recommendations = (await _dashboardService
            .GetVideoRecommendationsAsync(preferredExclude, count, reuseWhenExhausted: false)
            .ConfigureAwait(true)).ToList();

        // Completar lote: en append no reutilizamos IDs ya mostrados; en refresh sí se permite cruce.
        if (recommendations.Count < count && !appendMode)
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

        return recommendations;
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
        CanLoadMoreRankedRecommendations = picked.Count > 0;
    }

    private async Task AppendRankedRecommendationsAsync()
    {
        const int count = RankedVideoRecommendation.DefaultPickCount;
        var otherPanelIds = GetRecommendedVideoIds();
        var preferredExclude = new HashSet<int>(otherPanelIds);
        preferredExclude.UnionWith(GetRankedRecommendedVideoIds());
        preferredExclude.UnionWith(_rankedSession.ShownMediaFileIds);

        var picked = (await _dashboardService
            .GetRankedVideoRecommendationsAsync(preferredExclude, count)
            .ConfigureAwait(true)).ToList();

        if (picked.Count == 0 && _rankedSession.ShownMediaFileIds.Count > 0)
        {
            // Catálogo agotado en este ciclo: no reiniciar solo por la flecha (el usuario usa «Otras»).
            CanLoadMoreRankedRecommendations = false;
            return;
        }

        if (picked.Count == 0)
        {
            CanLoadMoreRankedRecommendations = false;
            return;
        }

        await AppendRecommendationItemsAsync(picked, isRanked: true).ConfigureAwait(true);

        _rankedSession.SetCurrent(
            RankedRecommendedVideos.Select(item => item.Video.Id).ToArray());

        CanLoadMoreRankedRecommendations = picked.Count >= count;
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

        await AppendRecommendationItemsAsync(recommendations, isRanked: false).ConfigureAwait(true);
    }

    private async Task SetRankedRecommendationsAsync(IReadOnlyList<MediaFileViewStats> recommendations)
    {
        RankedRecommendedVideos.Clear();
        OnPropertyChanged(nameof(HasRankedRecommendation));

        if (recommendations.Count == 0)
            return;

        await AppendRecommendationItemsAsync(recommendations, isRanked: true).ConfigureAwait(true);
    }

    private Task AppendRecommendationItemsAsync(
        IReadOnlyList<MediaFileViewStats> recommendations,
        bool isRanked)
    {
        if (recommendations.Count == 0)
            return Task.CompletedTask;

        var generation = isRanked
            ? ++_rankedRecommendationThumbGeneration
            : ++_recommendationThumbGeneration;

        var target = isRanked ? RankedRecommendedVideos : RecommendedVideos;
        var items = recommendations
            .Select(video => new DashboardRecommendationItem(video, showTierLabel: isRanked))
            .ToList();

        foreach (var item in items)
            target.Add(item);

        OnPropertyChanged(isRanked ? nameof(HasRankedRecommendation) : nameof(HasRecommendation));

        // Miniaturas/resolución en segundo plano: no bloquean el fin de IsBusy ni el arranque.
        _ = LoadRecommendationDetailsAsync(items, generation, isRanked);
        return Task.CompletedTask;
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

}
