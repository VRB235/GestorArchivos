using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Selección ponderada de videos para la recomendación del Dashboard.
/// </summary>
public static class VideoRecommendation
{
    public const int DefaultPickCount = 5;

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
        IReadOnlyCollection<int> exclude = excludeMediaFileId is int id
            ? [id]
            : [];

        return PickWeightedMany(videos, exclude, count: 1, random).FirstOrDefault();
    }

    /// <summary>
    /// Elige hasta <paramref name="count"/> videos distintos con muestreo ponderado sin reemplazo.
    /// </summary>
    /// <param name="reuseWhenExhausted">
    /// Si es true y al excluir no queda nada, reutiliza el pool completo (útil al refrescar).
    /// Si es false, se detiene cuando no hay candidatos nuevos.
    /// </param>
    public static IReadOnlyList<MediaFileViewStats> PickWeightedMany(
        IReadOnlyList<MediaFileViewStats> videos,
        IReadOnlyCollection<int> excludeMediaFileIds,
        int count = DefaultPickCount,
        Random? random = null,
        bool reuseWhenExhausted = true)
    {
        if (videos.Count == 0 || count <= 0)
            return [];

        random ??= Random.Shared;
        var excluded = excludeMediaFileIds as HashSet<int>
            ?? excludeMediaFileIds.ToHashSet();

        var candidates = videos
            .Where(video => !excluded.Contains(video.Id))
            .ToList();

        if (candidates.Count == 0)
        {
            if (!reuseWhenExhausted)
                return [];

            candidates = videos.ToList();
        }

        var picks = new List<MediaFileViewStats>(Math.Min(count, candidates.Count));
        while (picks.Count < count && candidates.Count > 0)
        {
            var pick = PickWeightedFromCandidates(candidates, random);
            if (pick is null)
                break;

            picks.Add(pick);
            candidates.RemoveAll(video => video.Id == pick.Id);
        }

        return picks;
    }

    private static MediaFileViewStats? PickWeightedFromCandidates(
        IReadOnlyList<MediaFileViewStats> candidates,
        Random random)
    {
        if (candidates.Count == 0)
            return null;

        if (candidates.Count == 1)
            return candidates[0];

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
