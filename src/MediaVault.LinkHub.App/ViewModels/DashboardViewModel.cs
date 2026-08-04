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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRecommendation))]
    [NotifyPropertyChangedFor(nameof(RecommendedRankingStars))]
    [NotifyPropertyChangedFor(nameof(RecommendedOpenCountLabel))]
    private MediaFileViewStats? _recommendedVideo;

    [ObservableProperty]
    private ImageSource? _recommendedThumbnail;

    public bool HasRecommendation => RecommendedVideo is not null;

    public int RecommendedRankingStars =>
        MediaFileRankingScale.ToDisplayStars(RecommendedVideo?.RankingGlobal ?? 0);

    public string RecommendedOpenCountLabel
    {
        get
        {
            var opens = RecommendedVideo?.VecesAbierto ?? 0;
            return opens == 1 ? "1 apertura" : $"{opens} aperturas";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRankedRecommendation))]
    [NotifyPropertyChangedFor(nameof(RankedRecommendedRankingStars))]
    [NotifyPropertyChangedFor(nameof(RankedRecommendedOpenCountLabel))]
    [NotifyPropertyChangedFor(nameof(RankedTierLabel))]
    private MediaFileViewStats? _rankedRecommendedVideo;

    [ObservableProperty]
    private ImageSource? _rankedRecommendedThumbnail;

    public bool HasRankedRecommendation => RankedRecommendedVideo is not null;

    public int RankedRecommendedRankingStars =>
        MediaFileRankingScale.ToDisplayStars(RankedRecommendedVideo?.RankingGlobal ?? 0);

    public string RankedRecommendedOpenCountLabel
    {
        get
        {
            var opens = RankedRecommendedVideo?.VecesAbierto ?? 0;
            return opens == 1 ? "1 apertura" : $"{opens} aperturas";
        }
    }

    public string RankedTierLabel => RankedRecommendedVideo is null
        ? string.Empty
        : $"Entre videos con {RankedRecommendedRankingStars} ★";

    public Task InitializeAsync() =>
        RunBusyCoreAsync(async () =>
        {
            await ReloadAsync(preserveRecommendation: false).ConfigureAwait(true);
        }, "Actualizando estadísticas...");

    private async Task ReloadAsync(bool preserveRecommendation)
    {
        var keepMixed = preserveRecommendation && RecommendedVideo is not null;
        var keepRanked = preserveRecommendation && RankedRecommendedVideo is not null;
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

        if (keepMixed)
            TryRefreshRecommendationStats(RecommendedVideo!.Id, stats, isRanked: false);
        else
            await LoadRecommendationAsync(excludeMediaFileId: null).ConfigureAwait(true);

        if (keepRanked)
            TryRefreshRecommendationStats(RankedRecommendedVideo!.Id, stats, isRanked: true);
        else
            await LoadRankedRecommendationAsync(advance: false).ConfigureAwait(true);
    }

    private void TryRefreshRecommendationStats(int mediaFileId, DashboardStatistics stats, bool isRanked)
    {
        var updated = stats.Top10MostViewedVideos
            .Concat(stats.Top10BestRankedVideos)
            .FirstOrDefault(file => file.Id == mediaFileId);

        if (updated is null)
            return;

        if (isRanked)
        {
            RankedRecommendedVideo = updated;
            OnPropertyChanged(nameof(RankedRecommendedOpenCountLabel));
            OnPropertyChanged(nameof(RankedRecommendedRankingStars));
            OnPropertyChanged(nameof(RankedTierLabel));
        }
        else
        {
            RecommendedVideo = updated;
            OnPropertyChanged(nameof(RecommendedOpenCountLabel));
            OnPropertyChanged(nameof(RecommendedRankingStars));
        }
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
            () => LoadRecommendationAsync(RecommendedVideo?.Id),
            "Eligiendo otra recomendación...");

    [RelayCommand]
    private Task RefreshRankedRecommendationAsync() =>
        RunBusyCoreAsync(
            () => LoadRankedRecommendationAsync(advance: true),
            "Eligiendo otro rankeado...");

    [RelayCommand(CanExecute = nameof(HasRecommendation))]
    private async Task OpenRecommendedAsync()
    {
        if (RecommendedVideo is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(RecommendedVideo.Id).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo. Verifique que exista en disco.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasRecommendation))]
    private async Task OpenRecommendedWithVlcAsync()
    {
        if (RecommendedVideo is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService
                .OpenFileAsync(RecommendedVideo.Id, preferVlc: true)
                .ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasRankedRecommendation))]
    private async Task OpenRankedRecommendedAsync()
    {
        if (RankedRecommendedVideo is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService.OpenFileAsync(RankedRecommendedVideo.Id).ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo. Verifique que exista en disco.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(HasRankedRecommendation))]
    private async Task OpenRankedRecommendedWithVlcAsync()
    {
        if (RankedRecommendedVideo is null)
            return;

        await ExecuteBusyAsync(async () =>
        {
            var opened = await _mediaVaultService
                .OpenFileAsync(RankedRecommendedVideo.Id, preferVlc: true)
                .ConfigureAwait(true);
            if (opened is null)
                throw new InvalidOperationException("No se pudo abrir el archivo con VLC.");

            await ReloadAsync(preserveRecommendation: true).ConfigureAwait(true);
        }, "Abriendo con VLC...").ConfigureAwait(true);
    }

    private async Task LoadRecommendationAsync(int? excludeMediaFileId)
    {
        var recommendation = await _dashboardService
            .GetVideoRecommendationAsync(excludeMediaFileId)
            .ConfigureAwait(true);
        await SetRecommendationAsync(recommendation).ConfigureAwait(true);
    }

    /// <param name="advance">
    /// false: restaurar el actual de sesión o elegir el primero.
    /// true: pedir otro distinto (sin repetir los ya mostrados; al agotar, reinicia el ciclo).
    /// </param>
    private async Task LoadRankedRecommendationAsync(bool advance)
    {
        if (!advance && _rankedSession.CurrentMediaFileId is int currentId)
        {
            var restored = await _dashboardService.GetVideoStatsByIdAsync(currentId).ConfigureAwait(true);
            if (restored is not null && MediaFileRankingScale.ToDisplayStars(restored.RankingGlobal) > 0)
            {
                await SetRankedRecommendationAsync(restored).ConfigureAwait(true);
                return;
            }
        }

        var picked = await _dashboardService
            .GetRankedVideoRecommendationAsync(_rankedSession.ShownMediaFileIds)
            .ConfigureAwait(true);

        if (picked is null && _rankedSession.ShownMediaFileIds.Count > 0)
        {
            _rankedSession.Reset();
            picked = await _dashboardService
                .GetRankedVideoRecommendationAsync(_rankedSession.ShownMediaFileIds)
                .ConfigureAwait(true);
        }

        if (picked is not null)
            _rankedSession.SetCurrent(picked.Id);

        await SetRankedRecommendationAsync(picked).ConfigureAwait(true);
    }

    partial void OnRecommendedVideoChanged(MediaFileViewStats? value)
    {
        OpenRecommendedCommand.NotifyCanExecuteChanged();
        OpenRecommendedWithVlcCommand.NotifyCanExecuteChanged();
    }

    partial void OnRankedRecommendedVideoChanged(MediaFileViewStats? value)
    {
        OpenRankedRecommendedCommand.NotifyCanExecuteChanged();
        OpenRankedRecommendedWithVlcCommand.NotifyCanExecuteChanged();
    }

    private async Task SetRecommendationAsync(MediaFileViewStats? recommendation)
    {
        RecommendedVideo = recommendation;
        RecommendedThumbnail = null;

        if (recommendation is null)
            return;

        var generation = ++_recommendationThumbGeneration;
        var thumbnail = await LoadParentFolderThumbnailAsync(recommendation.Path, 200).ConfigureAwait(true);

        if (generation != _recommendationThumbGeneration)
            return;

        RecommendedThumbnail = thumbnail;
    }

    private async Task SetRankedRecommendationAsync(MediaFileViewStats? recommendation)
    {
        RankedRecommendedVideo = recommendation;
        RankedRecommendedThumbnail = null;

        if (recommendation is null)
            return;

        var generation = ++_rankedRecommendationThumbGeneration;
        var thumbnail = await LoadParentFolderThumbnailAsync(recommendation.Path, 200).ConfigureAwait(true);

        if (generation != _rankedRecommendationThumbGeneration)
            return;

        RankedRecommendedThumbnail = thumbnail;
    }

    /// <summary>
    /// Prioridad: imagen aleatoria de Pictures (sesión) → icono de carpeta → miniatura shell.
    /// </summary>
    private async Task<ImageSource?> LoadParentFolderThumbnailAsync(string videoPath, int size)
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

        var sessionPicture = await Task.Run(() =>
            FolderSessionPicturePicker.TryLoadSessionThumbnail(folderPath, size)).ConfigureAwait(true);
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
