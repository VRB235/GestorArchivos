namespace MediaVault.LinkHub.Application.Models.Dashboard;

/// <summary>
/// Resumen de un archivo para el ranking Top 10 del Dashboard.
/// </summary>
public sealed class MediaFileViewStats
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public int VecesAbierto { get; init; }

    public double RankingGlobal { get; init; }

    public bool IsVideo { get; init; }

    public string? CategoryName { get; init; }
}
