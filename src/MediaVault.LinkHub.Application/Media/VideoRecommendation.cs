using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Selección ponderada de videos para la recomendación del Dashboard.
/// </summary>
public static class VideoRecommendation
{
    /// <summary>
    /// score = RankingGlobal * 3 + Log(1 + VecesAbierto) * 1.5 + Random(0..2)
    /// </summary>
    public static double ComputeScore(MediaFileViewStats video, double randomNoise) =>
        video.RankingGlobal * 3.0
        + Math.Log(1 + Math.Max(0, video.VecesAbierto)) * 1.5
        + Math.Clamp(randomNoise, 0, 2);

    public static MediaFileViewStats? PickWeighted(
        IReadOnlyList<MediaFileViewStats> videos,
        int? excludeMediaFileId,
        Random? random = null)
    {
        if (videos.Count == 0)
            return null;

        var candidates = excludeMediaFileId is int excludeId && videos.Count > 1
            ? videos.Where(video => video.Id != excludeId).ToList()
            : videos.ToList();

        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

        random ??= Random.Shared;
        var scored = candidates
            .Select(video => (Video: video, Score: ComputeScore(video, random.NextDouble() * 2.0)))
            .ToList();

        var total = scored.Sum(item => item.Score);
        if (total <= 0)
            return candidates[random.Next(candidates.Count)];

        var pick = random.NextDouble() * total;
        var cumulative = 0.0;
        foreach (var item in scored)
        {
            cumulative += item.Score;
            if (pick <= cumulative)
                return item.Video;
        }

        return scored[^1].Video;
    }
}
