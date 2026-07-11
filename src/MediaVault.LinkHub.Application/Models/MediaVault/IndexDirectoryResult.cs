namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Resultado de una operación de indexación recursiva del vault.
/// </summary>
public sealed class IndexDirectoryResult
{
    public int FilesIndexed { get; init; }

    public int FilesAdded { get; init; }

    public int FilesUpdated { get; init; }

    public int FilesSkipped { get; init; }

    public string RootPath { get; init; } = string.Empty;
}
