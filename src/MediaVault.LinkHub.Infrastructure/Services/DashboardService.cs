using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.Dashboard;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class DashboardService : IDashboardService
{
  private readonly IDbContextFactory<AppDbContext> _contextFactory;

  public DashboardService(IDbContextFactory<AppDbContext> contextFactory)
  {
    _contextFactory = contextFactory;
  }

  public async Task<IReadOnlyList<MediaFileViewStats>> GetTop10MostViewedAsync(
    CancellationToken cancellationToken = default)
  {
    var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
    return stats.Top10MostViewed;
  }

  public async Task<IReadOnlyList<CategoryDistributionItem>> GetLinkDistributionByCategoryAsync(
    CancellationToken cancellationToken = default)
  {
    var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
    return stats.LinkDistributionByCategory;
  }

  public async Task<double> GetAverageGlobalRankingAsync(CancellationToken cancellationToken = default)
  {
    var stats = await GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
    return stats.AverageGlobalRanking;
  }

  public async Task<DashboardStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var rows = await context.MediaFiles
      .AsNoTracking()
      .Include(file => file.Categories)
      .Select(file => new
      {
        file.Id,
        file.Name,
        file.Extension,
        file.Path,
        file.VecesAbierto,
        file.RankingCalidad,
        file.RankingContenido,
        file.RankingGusto,
        CategoryNames = file.Categories
          .OrderBy(category => category.Name)
          .Select(category => category.Name)
          .ToList()
      })
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    var allStats = rows.Select(row => MapToStats(
      row.Id,
      row.Name,
      row.Extension,
      row.Path,
      row.VecesAbierto,
      row.RankingCalidad,
      row.RankingContenido,
      row.RankingGusto,
      FormatCategoryNames(row.CategoryNames))).ToList();

    // Tops / métricas de disco: excluir papelera y rutas inexistentes (evita stickers de $RECYCLE.BIN).
    var presentStats = allStats
      .Where(file => MediaPathEligibility.ExistsSafely(file.Path))
      .ToList();
    var videos = presentStats.Where(file => file.IsVideo).ToList();
    var photos = presentStats.Where(file => !file.IsVideo).ToList();
    var ranked = presentStats.Where(file => file.RankingGlobal > 0).ToList();
    var rankedVideos = ranked.Where(file => file.IsVideo).ToList();
    var rankedPhotos = ranked.Where(file => !file.IsVideo).ToList();

    var groupedLinks = await context.WebLinks
      .AsNoTracking()
      .GroupBy(link => link.Categoria)
      .Select(group => new { Categoria = group.Key, Count = group.Count() })
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    var linkDistribution = Enum.GetValues<LinkCategory>()
      .Select(category => new CategoryDistributionItem
      {
        Categoria = category,
        Count = groupedLinks.FirstOrDefault(item => item.Categoria == category)?.Count ?? 0
      })
      .ToList();

    var mediaCategoryDistribution = rows
      .Where(row => MediaPathEligibility.ExistsSafely(row.Path))
      .SelectMany(row =>
      {
        if (row.CategoryNames.Count == 0)
          return new[] { new { Label = "Sin categoría", FileId = row.Id } };

        return row.CategoryNames.Select(name => new { Label = name, FileId = row.Id });
      })
      .GroupBy(item => item.Label)
      .Select(group => new MediaDistributionItem
      {
        Label = group.Key,
        Count = group.Select(item => item.FileId).Distinct().Count()
      })
      .OrderByDescending(item => item.Count)
      .ThenBy(item => item.Label)
      .ToList();

    var averageRankingByCategory = rows
      .Where(row => MediaPathEligibility.ExistsSafely(row.Path))
      .SelectMany(row =>
      {
        var ranking = MediaFileRankingScale.ComputeGlobal(
          row.RankingCalidad,
          row.RankingContenido,
          row.RankingGusto);

        if (row.CategoryNames.Count == 0 || ranking <= 0)
          return Enumerable.Empty<(string CategoryName, double Ranking)>();

        return row.CategoryNames.Select(name => (CategoryName: name, Ranking: ranking));
      })
      .GroupBy(item => item.CategoryName)
      .Select(group => new MediaCategoryRankingItem
      {
        CategoryName = group.Key,
        AverageRanking = group.Average(item => item.Ranking),
        VideoCount = group.Count()
      })
      .OrderByDescending(item => item.AverageRanking)
      .ThenBy(item => item.CategoryName)
      .ToList();

    var totalWebLinks = await context.WebLinks.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);
    var totalQuickNotes = await context.QuickNotes.AsNoTracking().CountAsync(cancellationToken).ConfigureAwait(false);

    return new DashboardStatistics
    {
      Top10MostViewed = TakeTopByViews(presentStats, 10),
      Top10MostViewedVideos = TakeTopByViews(videos, 10),
      Top10MostViewedPhotos = TakeTopByViews(photos, 10),
      Top10BestRankedVideos = TakeTopByRanking(rankedVideos, 10),
      Top10BestRankedPhotos = TakeTopByRanking(rankedPhotos, 10),
      LinkDistributionByCategory = linkDistribution,
      VideoDistributionByCategory = mediaCategoryDistribution,
      AverageRankingByVideoCategory = averageRankingByCategory,
      AverageGlobalRanking = ranked.Count == 0 ? 0 : ranked.Average(file => file.RankingGlobal),
      AverageVideoRanking = rankedVideos.Count == 0 ? 0 : rankedVideos.Average(file => file.RankingGlobal),
      AveragePhotoRanking = rankedPhotos.Count == 0 ? 0 : rankedPhotos.Average(file => file.RankingGlobal),
      TotalMediaFiles = presentStats.Count,
      TotalVideos = videos.Count,
      TotalPhotos = photos.Count,
      TotalWebLinks = totalWebLinks,
      TotalQuickNotes = totalQuickNotes,
      TotalVideoOpens = videos.Sum(file => file.VecesAbierto),
      VideosNeverOpened = videos.Count(file => file.VecesAbierto <= 0),
      VideosUnrated = videos.Count(file => file.RankingGlobal <= 0)
    };
  }

  public async Task<IReadOnlyList<MediaFileViewStats>> GetVideoRecommendationsAsync(
    IReadOnlyCollection<int>? excludeMediaFileIds = null,
    int count = VideoRecommendation.DefaultPickCount,
    bool reuseWhenExhausted = true,
    CancellationToken cancellationToken = default)
  {
    var videos = await LoadVideoStatsAsync(cancellationToken).ConfigureAwait(false);
    return VideoRecommendation.PickWeightedMany(
      videos,
      excludeMediaFileIds ?? [],
      count,
      random: null,
      reuseWhenExhausted);
  }

  public async Task<IReadOnlyList<MediaFileViewStats>> GetRankedVideoRecommendationsAsync(
    IReadOnlyCollection<int> excludeMediaFileIds,
    int count = RankedVideoRecommendation.DefaultPickCount,
    CancellationToken cancellationToken = default)
  {
    var videos = await LoadVideoStatsAsync(cancellationToken).ConfigureAwait(false);
    return RankedVideoRecommendation.PickByStarTiersMany(videos, excludeMediaFileIds, count);
  }

  public async Task<MediaFileViewStats?> GetVideoStatsByIdAsync(
    int mediaFileId,
    CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var row = await context.MediaFiles
      .AsNoTracking()
      .Where(file => file.Id == mediaFileId)
      .Select(file => new
      {
        file.Id,
        file.Name,
        file.Extension,
        file.Path,
        file.VecesAbierto,
        file.RankingCalidad,
        file.RankingContenido,
        file.RankingGusto,
        CategoryNames = file.Categories
          .OrderBy(category => category.Name)
          .Select(category => category.Name)
          .ToList()
      })
      .FirstOrDefaultAsync(cancellationToken)
      .ConfigureAwait(false);

    if (row is null)
      return null;

    var stats = MapToStats(
      row.Id,
      row.Name,
      row.Extension,
      row.Path,
      row.VecesAbierto,
      row.RankingCalidad,
      row.RankingContenido,
      row.RankingGusto,
      FormatCategoryNames(row.CategoryNames));

    if (!stats.IsVideo || !MediaPathEligibility.ExistsSafely(stats.Path))
      return null;

    return stats;
  }

  private async Task<List<MediaFileViewStats>> LoadVideoStatsAsync(CancellationToken cancellationToken)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var rows = await context.MediaFiles
      .AsNoTracking()
      .Include(file => file.Categories)
      .Select(file => new
      {
        file.Id,
        file.Name,
        file.Extension,
        file.Path,
        file.VecesAbierto,
        file.RankingCalidad,
        file.RankingContenido,
        file.RankingGusto,
        CategoryNames = file.Categories
          .OrderBy(category => category.Name)
          .Select(category => category.Name)
          .ToList()
      })
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);

    // Primero descarta papelera/rutas inválidas (barato); luego Exists solo sobre candidatas.
    // Así no recomendamos stickers de $RECYCLE.BIN ni se consulta Shell sobre ellas (crash en Release).
    return rows
      .Select(row => MapToStats(
        row.Id,
        row.Name,
        row.Extension,
        row.Path,
        row.VecesAbierto,
        row.RankingCalidad,
        row.RankingContenido,
        row.RankingGusto,
        FormatCategoryNames(row.CategoryNames)))
      .Where(file => file.IsVideo)
      .Where(file => MediaPathEligibility.ExistsSafely(file.Path))
      .ToList();
  }

  private static MediaFileViewStats MapToStats(
    int id,
    string name,
    string extension,
    string path,
    int vecesAbierto,
    double rankingCalidad,
    double rankingContenido,
    double rankingGusto,
    string? categoryName) =>
    new()
    {
      Id = id,
      Name = name,
      Path = path,
      Extension = extension,
      VecesAbierto = vecesAbierto,
      RankingGlobal = MediaFileRankingScale.ComputeGlobal(rankingCalidad, rankingContenido, rankingGusto),
      IsVideo = MediaFileExtensions.IsVideo(path),
      CategoryName = categoryName
    };

  private static string? FormatCategoryNames(IReadOnlyList<string> categoryNames) =>
    categoryNames.Count == 0 ? null : string.Join(", ", categoryNames);

  private static IReadOnlyList<MediaFileViewStats> TakeTopByViews(
    IEnumerable<MediaFileViewStats> items,
    int count) =>
    items
      .Where(file => file.VecesAbierto > 0)
      .OrderByDescending(file => file.VecesAbierto)
      .ThenBy(file => file.Name)
      .Take(count)
      .ToList();

  private static IReadOnlyList<MediaFileViewStats> TakeTopByRanking(
    IEnumerable<MediaFileViewStats> items,
    int count) =>
    items
      .OrderByDescending(file => file.RankingGlobal)
      .ThenByDescending(file => file.VecesAbierto)
      .ThenBy(file => file.Name)
      .Take(count)
      .ToList();
}
