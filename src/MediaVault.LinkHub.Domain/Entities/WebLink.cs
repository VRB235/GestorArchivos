using MediaVault.LinkHub.Domain.Common;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Enlace web gestionado por el módulo Link Manager.
/// </summary>
public class WebLink : EntityBase
{
    public string Nombre { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Ruta local al logo/icono del enlace (opcional).
    /// </summary>
    public string? LogoPath { get; set; }

    public LinkCategory Categoria { get; set; } = LinkCategory.Oficial;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha en que el usuario marcó haber visitado/revisado el sitio (no es timestamp de BD).
    /// </summary>
    public DateTime? FechaUltimaActualizacion { get; set; }
}
