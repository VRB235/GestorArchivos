using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Tile de enlace de actriz con logo resuelto desde Link Manager / productora asociada.
/// </summary>
public sealed class ActressLinkTileItem
{
    public required ActressLink Link { get; init; }

    public required WebLink? ResolvedWebLink { get; init; }

    public string Title => Link.Title;

    public ActressLinkAction Action => Link.Action;

    public string? LogoPath => ResolvedWebLink?.LogoPath;

    public LinkCategory Category => ResolvedWebLink?.Categoria ?? LinkCategory.Oficial;

    public double LogoZoom => ResolvedWebLink?.LogoZoom ?? 1.0;

    public double LogoOffsetX => ResolvedWebLink?.LogoOffsetX ?? 0;

    public double LogoOffsetY => ResolvedWebLink?.LogoOffsetY ?? 0;
}
