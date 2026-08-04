using System.Diagnostics;
using Microsoft.Win32;

namespace MediaVault.LinkHub.Infrastructure.Launchers;

/// <summary>
/// Abre URLs en Firefox (ventana normal) en Windows.
/// </summary>
internal static class BrowserLauncher
{
  private static readonly string[] FirefoxCandidates =
  [
    @"C:\Program Files\Mozilla Firefox\firefox.exe",
    @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
  ];

  public static bool TryOpenInFirefox(string url)
  {
    if (!TryNormalizeUrl(url, out var normalizedUrl))
      return false;

    if (OperatingSystem.IsWindows())
    {
      foreach (var executablePath in EnumerateFirefoxExecutables())
      {
        if (TryStartProcess(executablePath, $"\"{normalizedUrl}\""))
          return true;
      }
    }

    return TryShellOpen(normalizedUrl);
  }

  private static IEnumerable<string> EnumerateFirefoxExecutables()
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var candidate in FirefoxCandidates)
    {
      if (File.Exists(candidate) && seen.Add(candidate))
        yield return candidate;
    }

    var fromRegistry = TryGetFirefoxFromRegistry();
    if (!string.IsNullOrWhiteSpace(fromRegistry)
        && File.Exists(fromRegistry)
        && seen.Add(fromRegistry))
    {
      yield return fromRegistry;
    }
  }

  private static string? TryGetFirefoxFromRegistry()
  {
    if (!OperatingSystem.IsWindows())
      return null;

    string[] keys =
    [
      @"SOFTWARE\Mozilla\Mozilla Firefox",
      @"SOFTWARE\WOW6432Node\Mozilla\Mozilla Firefox"
    ];

    foreach (var keyPath in keys)
    {
      using var key = Registry.LocalMachine.OpenSubKey(keyPath);
      if (key is null)
        continue;

      var currentVersion = key.GetValue("CurrentVersion") as string;
      if (string.IsNullOrWhiteSpace(currentVersion))
        continue;

      using var mainKey = key.OpenSubKey($@"{currentVersion}\Main");
      var pathToExe = mainKey?.GetValue("PathToExe") as string;
      if (!string.IsNullOrWhiteSpace(pathToExe))
        return pathToExe;
    }

    return null;
  }

  private static bool TryNormalizeUrl(string url, out string normalizedUrl)
  {
    normalizedUrl = url.Trim();

    if (!normalizedUrl.Contains("://", StringComparison.Ordinal))
      normalizedUrl = $"https://{normalizedUrl}";

    if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
      return false;

    if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
      return false;

    normalizedUrl = uri.AbsoluteUri;
    return true;
  }

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

  private static bool TryShellOpen(string url)
  {
    try
    {
      using var process = Process.Start(new ProcessStartInfo
      {
        FileName = url,
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
