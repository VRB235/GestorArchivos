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
}
