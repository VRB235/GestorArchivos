namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Opción de categoría para ComboBox de asignación a un video.
/// </summary>
public sealed record VideoCategoryOption(int? Id, string Name)
{
    public static VideoCategoryOption Uncategorized { get; } = new(null, "Sin categoría");
}

/// <summary>
/// Opción de filtro por categoría en el explorador.
/// </summary>
public sealed record VideoCategoryFilterOption(string Label, int? CategoryId)
{
    public const int UncategorizedSentinel = 0;

    public static VideoCategoryFilterOption All { get; } = new("Todas las categorías", null);

    public static VideoCategoryFilterOption Uncategorized { get; } = new("Sin categoría", UncategorizedSentinel);
}
