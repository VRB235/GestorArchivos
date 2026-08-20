namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Resultado de limpiar entradas inválidas del índice multimedia (sin borrar archivos en disco).
/// </summary>
public sealed class IndexPurgeResult
{
    public int RemovedUnusablePaths { get; init; }

    public int RemovedOutsideRoot { get; init; }

    public int RemovedMissingFiles { get; init; }

    public int RemovedTotal => RemovedUnusablePaths + RemovedOutsideRoot + RemovedMissingFiles;

    public bool HasChanges => RemovedTotal > 0;

    /// <summary>Ruta del respaldo creado antes del borrado, si aplica.</summary>
    public string? BackupFilePath { get; init; }
}
