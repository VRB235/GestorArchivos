namespace MediaVault.LinkHub.Application.Models.Dashboard;

/// <summary>
/// Ranking promedio agregado por categoría de video.
/// </summary>
public sealed class MediaCategoryRankingItem
{
    public string CategoryName { get; init; } = string.Empty;

    public double AverageRanking { get; init; }

    public int VideoCount { get; init; }
}
