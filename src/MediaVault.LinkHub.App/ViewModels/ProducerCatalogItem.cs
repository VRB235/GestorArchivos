using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Tile de productora en el catálogo con logo resuelto desde Link Manager.
/// </summary>
public sealed class ProducerCatalogItem
{
    public required Producer Producer { get; init; }

    public WebLink? PrimaryWebLink { get; init; }

    public int Id => Producer.Id;

    public string Name => Producer.Name;

    public string? LogoPath => PrimaryWebLink?.LogoPath;

    public LinkCategory Category => PrimaryWebLink?.Categoria ?? LinkCategory.Oficial;

    public double LogoZoom => PrimaryWebLink?.LogoZoom ?? 1.0;

    public double LogoOffsetX => PrimaryWebLink?.LogoOffsetX ?? 0;

    public double LogoOffsetY => PrimaryWebLink?.LogoOffsetY ?? 0;
}
