using MediaVault.LinkHub.Domain.Common;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Sugerencia de mejora o reporte de error del aplicativo.
/// </summary>
public class Suggestion : EntityBase
{
    public string Texto { get; set; } = string.Empty;

    public SuggestionKind Tipo { get; set; } = SuggestionKind.Mejora;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    public bool Resuelto { get; set; }

    public DateTime? FechaResuelto { get; set; }

    public ICollection<SuggestionAttachment> Attachments { get; set; } = new List<SuggestionAttachment>();
}
