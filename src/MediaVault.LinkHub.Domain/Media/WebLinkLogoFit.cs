namespace MediaVault.LinkHub.Domain.Media;

/// <summary>
/// Normaliza zoom y offsets del logo de un enlace web.
/// </summary>
public static class WebLinkLogoFit
{
    /// <summary>Menor que 1 reduce la imagen para que quepa con margen; 1 la encaja completa en el cuadro.</summary>
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;
    public const double MinOffset = -1.0;
    public const double MaxOffset = 1.0;

    public static double ClampZoom(double zoom) =>
        Math.Clamp(zoom <= 0 ? MinZoom : zoom, MinZoom, MaxZoom);

    public static double ClampOffset(double offset) =>
        Math.Clamp(offset, MinOffset, MaxOffset);
}
