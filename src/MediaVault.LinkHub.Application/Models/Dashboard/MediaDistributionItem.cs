namespace MediaVault.LinkHub.Application.Models.Dashboard;

/// <summary>
/// Punto de datos genérico para gráficos de distribución (etiqueta + conteo).
/// </summary>
public sealed class MediaDistributionItem
{
    public string Label { get; init; } = string.Empty;

    public int Count { get; init; }
}
