namespace MediaVault.LinkHub.Infrastructure.Media;

/// <summary>
/// Descarta rutas no aptas para recomendaciones/gráficos (papelera, volúmenes de sistema, etc.).
/// </summary>
public static class MediaPathEligibility
{
  public static bool IsUsableMediaPath(string? path)
  {
    if (string.IsNullOrWhiteSpace(path))
      return false;

    if (path.Contains("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)
        || path.Contains("System Volume Information", StringComparison.OrdinalIgnoreCase))
    {
      return false;
    }

    try
    {
      _ = Path.GetFullPath(path);
    }
    catch
    {
      return false;
    }

    return true;
  }

  public static bool ExistsSafely(string? path)
  {
    if (!IsUsableMediaPath(path))
      return false;

    try
    {
      return File.Exists(path);
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// True si <paramref name="path"/> está bajo <paramref name="indexRootPath"/> (o es la raíz).
  /// </summary>
  public static bool IsUnderIndexRoot(string? path, string? indexRootPath)
  {
    if (!IsUsableMediaPath(path) || string.IsNullOrWhiteSpace(indexRootPath))
      return false;

    try
    {
      var fullPath = Path.GetFullPath(path!);
      var fullRoot = Path.GetFullPath(indexRootPath);

      if (string.Equals(fullPath, fullRoot, StringComparison.OrdinalIgnoreCase))
        return true;

      var rootPrefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;

      return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }
}
