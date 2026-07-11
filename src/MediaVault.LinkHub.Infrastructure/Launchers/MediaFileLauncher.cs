using System.Diagnostics;
using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.Infrastructure.Launchers;

/// <summary>
/// Abre archivos multimedia con el visor predeterminado o VLC.
/// </summary>
internal static class MediaFileLauncher
{
  public static bool TryOpen(string filePath, bool preferVlc)
  {
    if (!File.Exists(filePath))
      return false;

    if (preferVlc && MediaFileExtensions.IsVideo(filePath))
    {
      var vlcPath = ResolveVlcPath();
      if (vlcPath is not null
          && TryStartProcess(vlcPath, $"--no-qt-recentplay \"{filePath}\""))
        return true;
    }

    return TryShellOpen(filePath);
  }

  private static string? ResolveVlcPath() =>
    VlcPathResolver.Resolve();

  private static bool TryStartProcess(string fileName, string arguments)
  {
    try
    {
      using var process = Process.Start(new ProcessStartInfo
      {
        FileName = fileName,
        Arguments = arguments,
        UseShellExecute = false,
        CreateNoWindow = true
      });

      return process is not null;
    }
    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
    {
      return false;
    }
  }

  private static bool TryShellOpen(string filePath)
  {
    try
    {
      using var process = Process.Start(new ProcessStartInfo
      {
        FileName = filePath,
        UseShellExecute = true
      });

      return process is not null;
    }
    catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or FileNotFoundException)
    {
      return false;
    }
  }
}
