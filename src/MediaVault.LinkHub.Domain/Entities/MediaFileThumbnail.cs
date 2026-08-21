using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Imagen de miniatura asignada a un video. El picker de sesión elige una al azar
/// entre las N rutas asociadas al mismo <see cref="MediaFile"/>.
/// </summary>
public class MediaFileThumbnail : EntityBase
{
    public int MediaFileId { get; set; }

    public MediaFile MediaFile { get; set; } = null!;

    /// <summary>Ruta absoluta de la imagen (típicamente bajo <c>{carpeta}/Pictures</c>).</summary>
    public string ImagePath { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
