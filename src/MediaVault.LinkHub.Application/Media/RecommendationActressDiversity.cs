using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Evita repetir la misma actriz dentro de un lote de recomendaciones.
/// Videos sin actrices no bloquean a otros; un video con varias actrices
/// marca todas como usadas.
/// </summary>
public static class RecommendationActressDiversity
{
    public static bool ConflictsWithUsedActresses(
        MediaFileViewStats video,
        IReadOnlySet<int> usedActressIds)
    {
        if (usedActressIds.Count == 0 || video.ActressIds.Count == 0)
            return false;

        for (var i = 0; i < video.ActressIds.Count; i++)
        {
            if (usedActressIds.Contains(video.ActressIds[i]))
                return true;
        }

        return false;
    }

    public static void RegisterActresses(MediaFileViewStats video, HashSet<int> usedActressIds)
    {
        for (var i = 0; i < video.ActressIds.Count; i++)
            usedActressIds.Add(video.ActressIds[i]);
    }
}
