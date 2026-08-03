namespace MediaVault.LinkHub.Infrastructure.Media;

/// <summary>
/// Extensiones de imagen y video soportadas por el indexador del vault.
/// </summary>
public static class MediaFileExtensions
{
  private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
  {
    ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico", ".heic",
    ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v", ".mpeg", ".mpg", ".3gp"
  };

  public static bool IsSupported(string filePath) =>
    Supported.Contains(Path.GetExtension(filePath));

  public static bool IsVideo(string filePath)
  {
    var extension = Path.GetExtension(filePath);
    return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".wmv", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".flv", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".mpeg", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".mpg", StringComparison.OrdinalIgnoreCase)
      || extension.Equals(".3gp", StringComparison.OrdinalIgnoreCase);
  }

  public static bool IsImage(string filePath) =>
    IsSupported(filePath) && !IsVideo(filePath);
}
