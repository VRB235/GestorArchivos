using System.ComponentModel.DataAnnotations.Schema;
using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Archivo multimedia indexado por el módulo File &amp; Media Vault.
/// </summary>
public class MediaFile : EntityBase
{
    /// <summary>
    /// Ruta absoluta del archivo en disco.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Extension { get; set; } = string.Empty;

    public int VecesAbierto { get; set; }

    public double RankingCalidad { get; set; }

    public double RankingContenido { get; set; }

    public double RankingGusto { get; set; }

    /// <summary>
    /// Última vez que el usuario abrió el archivo desde la app (UTC).
    /// </summary>
    public DateTime? LastOpenedAt { get; set; }

    public ICollection<VideoCategory> Categories { get; set; } = [];

    public ICollection<Actress> Actresses { get; set; } = [];

    public ICollection<Producer> Producers { get; set; } = [];

    /// <summary>
    /// Miniaturas asignadas al video (N rutas); el picker elige una al azar por sesión.
    /// </summary>
    public ICollection<MediaFileThumbnail> Thumbnails { get; set; } = [];

    /// <summary>
    /// Promedio de los tres rankings (0-5 estrellas). No se persiste en base de datos.
    /// </summary>
    [NotMapped]
    public double RankingGlobal =>
        (RankingCalidad + RankingContenido + RankingGusto) / 3.0;

    /// <summary>
    /// Expresión reutilizable para consultas LINQ traducibles a SQL.
    /// </summary>
    public static double ComputeRankingGlobal(MediaFile file) =>
        (file.RankingCalidad + file.RankingContenido + file.RankingGusto) / 3.0;
}
