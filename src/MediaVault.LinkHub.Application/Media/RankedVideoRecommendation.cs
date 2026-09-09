using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Recomendación de videos rankeados por tiers de estrellas (5 → 1), aleatoria dentro del tier.
/// Si no hay suficientes calificados, completa el lote con el resto del catálogo.
/// </summary>
public static class RankedVideoRecommendation
{
    public const int DefaultPickCount = 5;

    /// <summary>
    /// Elige un video en el tier más alto disponible que aún no esté en <paramref name="excludeMediaFileIds"/>.
    /// </summary>
    public static MediaFileViewStats? PickByStarTiers(
        IReadOnlyList<MediaFileViewStats> videos,
        IReadOnlyCollection<int> excludeMediaFileIds,
        Random? random = null) =>
        PickByStarTiersMany(videos, excludeMediaFileIds, count: 1, random).FirstOrDefault();

    /// <summary>
    /// Elige hasta <paramref name="count"/> videos: primero calificados (tiers 5→1), luego relleno ponderado.
    /// </summary>
    public static IReadOnlyList<MediaFileViewStats> PickByStarTiersMany(
        IReadOnlyList<MediaFileViewStats> videos,
        IReadOnlyCollection<int> excludeMediaFileIds,
        int count = DefaultPickCount,
        Random? random = null)
    {
        if (count <= 0)
            return [];

        random ??= Random.Shared;
        var videoPool = videos.Where(video => video.IsVideo).ToList();
        if (videoPool.Count == 0)
            return [];

        var excluded = excludeMediaFileIds as HashSet<int>
            ?? excludeMediaFileIds.ToHashSet();

        var picks = new List<MediaFileViewStats>(count);
        var usedActresses = new HashSet<int>();
        while (picks.Count < count)
        {
            var pick = PickSingleByStarTiers(videoPool, excluded, usedActresses, random);
            if (pick is null)
                break;

            picks.Add(pick);
            excluded.Add(pick.Id);
            RecommendationActressDiversity.RegisterActresses(pick, usedActresses);
        }

        if (picks.Count < count)
        {
            var fillers = VideoRecommendation.PickWeightedMany(
                videoPool,
                excluded,
                count - picks.Count,
                random,
                reuseWhenExhausted: false,
                seedUsedActressIds: usedActresses);

            foreach (var filler in fillers)
            {
                picks.Add(filler);
                excluded.Add(filler.Id);
                RecommendationActressDiversity.RegisterActresses(filler, usedActresses);
            }
        }

        return picks;
    }

    private static MediaFileViewStats? PickSingleByStarTiers(
        IReadOnlyList<MediaFileViewStats> videos,
        HashSet<int> excluded,
        HashSet<int> usedActresses,
        Random random)
    {
        var rankedVideos = videos
            .Where(video => video.IsVideo)
            .Where(IsRankedForRecommendation)
            .ToList();

        if (rankedVideos.Count == 0)
            return null;

        for (var stars = MediaFileRankingScale.MaxStars; stars >= 1; stars--)
        {
            var pool = rankedVideos
                .Where(video =>
                    GetRecommendationTier(video) == stars
                    && !excluded.Contains(video.Id)
                    && !RecommendationActressDiversity.ConflictsWithUsedActresses(video, usedActresses))
                .ToList();

            if (pool.Count == 0)
                continue;

            return pool[random.Next(pool.Count)];
        }

        return null;
    }

    /// <summary>
    /// Incluye cualquier promedio &gt; 0; el tier mínimo es 1 (evita excluir calificaciones parciales
    /// cuyo promedio redondea a 0 estrellas en UI).
    /// </summary>
    public static bool IsRankedForRecommendation(MediaFileViewStats video) =>
        video.RankingGlobal > 0;

    public static int GetRecommendationTier(MediaFileViewStats video) =>
        Math.Max(1, MediaFileRankingScale.ToDisplayStars(video.RankingGlobal));

    public static int GetDisplayStars(MediaFileViewStats video) =>
        MediaFileRankingScale.ToDisplayStars(video.RankingGlobal);
}
