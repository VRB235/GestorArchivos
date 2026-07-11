using MediaVault.LinkHub.Application.Models.MediaVault;

namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Estadísticas de ranking agregadas para un directorio (un nivel).
/// </summary>
public sealed record MediaVaultDirectoryRanking(double? AverageGlobal, int RankedFileCount)
{
    public static MediaVaultDirectoryRanking Empty { get; } = new(null, 0);

    public static MediaVaultDirectoryRanking FromEntries(IEnumerable<MediaVaultBrowserEntry> entries)
    {
        var globals = entries
            .Where(entry => !entry.IsDirectory && entry.MediaFile is not null)
            .Select(entry => entry.MediaFile!)
            .Where(file => MediaFileRankingScale.HasAnyRanking(
                file.RankingCalidad,
                file.RankingContenido,
                file.RankingGusto))
            .Select(file => MediaFileRankingScale.ComputeGlobal(
                file.RankingCalidad,
                file.RankingContenido,
                file.RankingGusto))
            .ToList();

        return globals.Count == 0
            ? Empty
            : new MediaVaultDirectoryRanking(globals.Average(), globals.Count);
    }
}
