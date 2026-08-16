using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Contrato del módulo Dashboard &amp; Estadísticas: consultas agregadas para LiveCharts2.
/// </summary>
public interface IDashboardService
{
    Task<IReadOnlyList<MediaFileViewStats>> GetTop10MostViewedAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDistributionItem>> GetLinkDistributionByCategoryAsync(
        CancellationToken cancellationToken = default);

    Task<double> GetAverageGlobalRankingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todas las métricas del dashboard en una sola consulta optimizada.
    /// </summary>
    Task<DashboardStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomienda videos con muestreo ponderado por ranking, vistas y ruido aleatorio.
    /// </summary>
    /// <param name="reuseWhenExhausted">
    /// Si es true y no quedan candidatos tras excluir, reutiliza el pool (puede repetir IDs excluidos).
    /// </param>
    Task<IReadOnlyList<MediaFileViewStats>> GetVideoRecommendationsAsync(
        IReadOnlyCollection<int>? excludeMediaFileIds = null,
        int count = 5,
        bool reuseWhenExhausted = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomienda videos rankeados por tiers de estrellas (5→1), excluyendo IDs ya mostrados en la sesión.
    /// </summary>
    Task<IReadOnlyList<MediaFileViewStats>> GetRankedVideoRecommendationsAsync(
        IReadOnlyCollection<int> excludeMediaFileIds,
        int count = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene estadísticas de un video indexado por Id, o null si no existe / no es video.
    /// </summary>
    Task<MediaFileViewStats?> GetVideoStatsByIdAsync(
        int mediaFileId,
        CancellationToken cancellationToken = default);
}
