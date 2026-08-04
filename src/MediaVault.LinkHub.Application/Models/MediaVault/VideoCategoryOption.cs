namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Opción de categoría para ComboBox de asignación a un video.
/// </summary>
public sealed record VideoCategoryOption(int? Id, string Name)
{
    public static VideoCategoryOption Uncategorized { get; } = new(null, "Sin categoría");
}
