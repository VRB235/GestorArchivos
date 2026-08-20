using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Imagen adjunta a una sugerencia (ruta managed bajo AppData).
/// </summary>
public class SuggestionAttachment : EntityBase
{
    public int SuggestionId { get; set; }

    public Suggestion Suggestion { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
