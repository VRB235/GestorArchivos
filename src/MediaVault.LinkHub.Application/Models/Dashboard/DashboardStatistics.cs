namespace MediaVault.LinkHub.Application.Models.Dashboard;

/// <summary>
/// Agregado de métricas del Dashboard listo para enlazar con LiveCharts2.
/// </summary>
public sealed class DashboardStatistics
{
    public IReadOnlyList<MediaFileViewStats> Top10MostViewed { get; init; } = [];

    public IReadOnlyList<MediaFileViewStats> Top10MostViewedVideos { get; init; } = [];

    public IReadOnlyList<MediaFileViewStats> Top10MostViewedPhotos { get; init; } = [];

    public IReadOnlyList<MediaFileViewStats> Top10BestRankedVideos { get; init; } = [];

    public IReadOnlyList<MediaFileViewStats> Top10BestRankedPhotos { get; init; } = [];

    public IReadOnlyList<CategoryDistributionItem> LinkDistributionByCategory { get; init; } = [];

    public IReadOnlyList<MediaDistributionItem> VideoDistributionByCategory { get; init; } = [];

    public IReadOnlyList<MediaCategoryRankingItem> AverageRankingByVideoCategory { get; init; } = [];

    public double AverageGlobalRanking { get; init; }

    public double AverageVideoRanking { get; init; }

    public double AveragePhotoRanking { get; init; }

    public int TotalMediaFiles { get; init; }

    public int TotalVideos { get; init; }

    public int TotalPhotos { get; init; }

    public int TotalWebLinks { get; init; }

    public int TotalQuickNotes { get; init; }

    /// <summary>Suma de <c>VecesAbierto</c> solo en videos.</summary>
    public int TotalVideoOpens { get; init; }

    public int VideosNeverOpened { get; init; }

    public int VideosUnrated { get; init; }
}
