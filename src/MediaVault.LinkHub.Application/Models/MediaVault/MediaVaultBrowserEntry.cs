using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Elemento visible en el explorador jerárquico de Media Vault.
/// </summary>
public sealed class MediaVaultBrowserEntry
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsDirectory { get; init; }

    public string FileType { get; init; } = string.Empty;

    public DateTime? CreatedAtUtc { get; init; }

    public DateTime? ModifiedAtUtc { get; init; }

    public string? CustomIconPath { get; init; }

    public MediaFile? MediaFile { get; init; }
}
