using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Recomendación de videos rankeados por tiers de estrellas (5 → 1), aleatoria dentro del tier.
/// </summary>
public static class RankedVideoRecommendation
{
    /// <summary>
    /// Elige un video en el tier más alto disponible que aún no esté en <paramref name="excludeMediaFileIds"/>.
    /// </summary>
    public static MediaFileViewStats? PickByStarTiers(
        IReadOnlyList<MediaFileViewStats> videos,
        IReadOnlyCollection<int> excludeMediaFileIds,
        Random? random = null)
    {
        var rankedVideos = videos
            .Where(video => video.IsVideo)
            .Where(video => MediaFileRankingScale.ToDisplayStars(video.RankingGlobal) > 0)
            .ToList();

        if (rankedVideos.Count == 0)
            return null;

        random ??= Random.Shared;
        var excluded = excludeMediaFileIds as HashSet<int>
            ?? excludeMediaFileIds.ToHashSet();

        for (var stars = MediaFileRankingScale.MaxStars; stars >= 1; stars--)
        {
            var pool = rankedVideos
                .Where(video =>
                    MediaFileRankingScale.ToDisplayStars(video.RankingGlobal) == stars
                    && !excluded.Contains(video.Id))
                .ToList();

            if (pool.Count == 0)
                continue;

            return pool[random.Next(pool.Count)];
        }

        return null;
    }

    public static int GetDisplayStars(MediaFileViewStats video) =>
        MediaFileRankingScale.ToDisplayStars(video.RankingGlobal);
}
