namespace MediaVault.LinkHub.Application.Media;

/// <summary>
/// Escala de ranking por estrellas (0-5) persistida en <see cref="Domain.Entities.MediaFile"/>.
/// </summary>
public static class MediaFileRankingScale
{
    public const int MaxStars = 5;

    public static int ToStars(double storedValue)
    {
        if (storedValue <= 0)
            return 0;

        if (storedValue > MaxStars)
        {
            return (int)Math.Clamp(
                Math.Round(storedValue / 2.0, MidpointRounding.AwayFromZero),
                1,
                MaxStars);
        }

        return (int)Math.Clamp(
            Math.Round(storedValue, MidpointRounding.AwayFromZero),
            0,
            MaxStars);
    }

    public static double ToStorage(int stars) =>
        Math.Clamp(stars, 0, MaxStars);

    public static double Normalize(double storedValue) =>
        ToStorage(ToStars(storedValue));

    public static double ComputeGlobal(double calidad, double contenido, double gusto) =>
        (Normalize(calidad) + Normalize(contenido) + Normalize(gusto)) / 3.0;

    public static double ComputeGlobal(int calidadStars, int contenidoStars, int gustoStars) =>
        (ToStorage(calidadStars) + ToStorage(contenidoStars) + ToStorage(gustoStars)) / 3.0;

    public static bool HasAnyRanking(double calidad, double contenido, double gusto) =>
        ToStars(calidad) > 0 || ToStars(contenido) > 0 || ToStars(gusto) > 0;

    public static int ToDisplayStars(double average) =>
        (int)Math.Clamp(
            Math.Round(average, MidpointRounding.AwayFromZero),
            0,
            MaxStars);
}
