using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MediaVault.LinkHub.App.Charts;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.Dashboard;
using MediaVault.LinkHub.Application.Services;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IDashboardService _dashboardService;
    private readonly IMediaVaultService _mediaVaultService;

    public DashboardViewModel(IDashboardService dashboardService, IMediaVaultService mediaVaultService)
    {
        _dashboardService = dashboardService;
        _mediaVaultService = mediaVaultService;
    }

    public string Title => "Dashboard & Estadísticas";

    public string Subtitle => "Métricas del sistema con gráficos LiveCharts2";

    public ObservableCollection<DashboardChartPanelViewModel> ChartSections { get; } = [];

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

    public Task InitializeAsync() =>
        RunBusyCoreAsync(() => ReloadAsync(), "Actualizando estadísticas...");

    private async Task ReloadAsync()
    {
        var stats = await _dashboardService.GetStatisticsAsync().ConfigureAwait(true);

        AverageGlobalRanking = stats.AverageGlobalRanking;
        AverageVideoRanking = stats.AverageVideoRanking;
        AveragePhotoRanking = stats.AveragePhotoRanking;
        TotalMediaFiles = stats.TotalMediaFiles;
        TotalVideos = stats.TotalVideos;
        TotalPhotos = stats.TotalPhotos;
        TotalWebLinks = stats.TotalWebLinks;
        TotalQuickNotes = stats.TotalQuickNotes;

        BuildChartSections(stats);
    }

    private void BuildChartSections(DashboardStatistics stats)
    {
        ChartSections.Clear();

        AddColumnChart(
            "Ranking global (0-5 estrellas)",
            "Sin archivos calificados. Asigne estrellas en Media Vault.",
            stats.AverageGlobalRanking > 0,
            panel =>
            {
                DashboardChartFactory.PopulateRankingChart(
                    stats.AverageGlobalRanking,
                    panel.Series,
                    out var xAxes,
                    out var yAxes);
                panel.XAxes = xAxes;
                panel.YAxes = yAxes;
            });

        AddBarChart(
            "Top 10 videos más vistos",
            "Sin videos abiertos desde la app. Abra videos en Media Vault.",
            stats.Top10MostViewedVideos,
            panel => DashboardChartFactory.PopulateTopViewsChart(
                stats.Top10MostViewedVideos,
                panel,
                onPointClicked: index => _ = OpenChartMediaFileAsync(panel, index)));

        AddBarChart(
            "Top 10 fotos más vistas",
            "Sin fotos abiertas desde la app. Abra imágenes en Media Vault.",
            stats.Top10MostViewedPhotos,
            panel => DashboardChartFactory.PopulateTopViewsChart(
                stats.Top10MostViewedPhotos,
                panel,
                SkiaSharp.SKColor.Parse("#10B981"),
                index => _ = OpenChartMediaFileAsync(panel, index)));

        AddRankedBarChart(
            "Top 10 videos mejor rankeados",
            "Sin videos calificados. Use estrellas en Media Vault.",
            stats.Top10BestRankedVideos,
            panel => DashboardChartFactory.PopulateTopRankedChart(
                stats.Top10BestRankedVideos,
                panel,
                onPointClicked: index => _ = OpenChartMediaFileAsync(panel, index)));

        AddRankedBarChart(
            "Top 10 fotos mejor rankeadas",
            "Sin fotos calificadas. Use estrellas en Media Vault.",
            stats.Top10BestRankedPhotos,
            panel => DashboardChartFactory.PopulateTopRankedChart(
                stats.Top10BestRankedPhotos,
                panel,
                SkiaSharp.SKColor.Parse("#F59E0B"),
                index => _ = OpenChartMediaFileAsync(panel, index)));

        AddPieChart(
            "Archivos por categoría",
            "Sin archivos indexados o sin categorías asignadas.",
            stats.VideoDistributionByCategory.Any(item => item.Count > 0),
            panel => DashboardChartFactory.PopulateMediaDistributionPieChart(
                stats.VideoDistributionByCategory,
                panel.Series));

        AddColumnChart(
            "Ranking promedio por categoría",
            "Califique archivos con categoría para ver este gráfico.",
            stats.AverageRankingByVideoCategory.Count > 0,
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
            "Enlaces por categoría",
            "Sin enlaces registrados. Agregue enlaces en Link Manager.",
            stats.LinkDistributionByCategory.Any(item => item.Count > 0),
            panel => DashboardChartFactory.PopulateCategoryPieChart(
                stats.LinkDistributionByCategory,
                panel.Series));

        AddBarChart(
            "Top 10 archivos más vistos (todos)",
            "Sin datos de visualización. Indexe archivos y ábralos desde Media Vault.",
            stats.Top10MostViewed,
            panel => DashboardChartFactory.PopulateTopViewsChart(
                stats.Top10MostViewed,
                panel,
                SkiaSharp.SKColor.Parse("#06B6D4"),
                index => _ = OpenChartMediaFileAsync(panel, index)));
    }

    private void AddBarChart(
        string title,
        string emptyMessage,
        IReadOnlyList<MediaFileViewStats> data,
        Action<DashboardChartPanelViewModel> populate)
    {
        var panel = CreatePanel(title, emptyMessage, isPie: false, data.Count > 0);
        panel.MediaFiles = data.ToArray();
        populate(panel);
        ChartSections.Add(panel);
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

            await ReloadAsync().ConfigureAwait(true);
        }, "Abriendo archivo...").ConfigureAwait(true);
    }

    private void AddRankedBarChart(
        string title,
        string emptyMessage,
        IReadOnlyList<MediaFileViewStats> data,
        Action<DashboardChartPanelViewModel> populate) =>
        AddBarChart(title, emptyMessage, data, populate);

    private void AddColumnChart(
        string title,
        string emptyMessage,
        bool hasData,
        Action<DashboardChartPanelViewModel> populate)
    {
        var panel = CreatePanel(title, emptyMessage, isPie: false, hasData);
        populate(panel);
        ChartSections.Add(panel);
    }

    private void AddPieChart(
        string title,
        string emptyMessage,
        bool hasData,
        Action<DashboardChartPanelViewModel> populate)
    {
        var panel = CreatePanel(title, emptyMessage, isPie: true, hasData);
        populate(panel);
        ChartSections.Add(panel);
    }

    private DashboardChartPanelViewModel CreatePanel(
        string title,
        string emptyMessage,
        bool isPie,
        bool hasData) =>
        new()
        {
            Title = title,
            EmptyMessage = emptyMessage,
            IsPie = isPie,
            HasData = hasData,
            DrawMarginFrame = DashboardChartFactory.CreateDrawMarginFrame()
        };
}
