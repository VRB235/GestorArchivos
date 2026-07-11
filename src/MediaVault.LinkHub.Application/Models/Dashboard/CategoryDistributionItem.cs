using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Application.Models.Dashboard;

/// <summary>
/// Punto de datos para gráficos de distribución de enlaces por categoría (LiveCharts2).
/// </summary>
public sealed class CategoryDistributionItem
{
    public LinkCategory Categoria { get; init; }

    public string Label => Categoria.ToString();

    public int Count { get; init; }
}
