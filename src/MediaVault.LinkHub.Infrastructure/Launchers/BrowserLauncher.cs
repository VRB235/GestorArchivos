using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace MediaVault.LinkHub.Infrastructure.Launchers;

/// <summary>
/// Abre URLs en el navegador predeterminado en modo privado/incógnito (Windows).
/// </summary>
internal static class BrowserLauncher
{
  private static readonly (string ExecutablePath, string ArgumentPrefix)[] FallbackBrowsers =
  [
    (@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", "--inprivate"),
    (@"C:\Program Files\Microsoft\Edge\Application\msedge.exe", "--inprivate"),
    (@"C:\Program Files\Google\Chrome\Application\chrome.exe", "--incognito"),
    (@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe", "--incognito"),
    (@"C:\Program Files\Mozilla Firefox\firefox.exe", "-private-window"),
    (@"C:\Program Files (x86)\Mozilla Firefox\firefox.exe", "-private-window"),
    (@"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe", "--incognito"),
    (@"C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application\brave.exe", "--incognito")
  ];

  public static bool TryOpenInPrivateWindow(string url)
  {
    if (!TryNormalizeUrl(url, out var normalizedUrl))
      return false;

    if (OperatingSystem.IsWindows())
    {
      if (TryOpenWithDefaultBrowserIncognito(normalizedUrl))
        return true;

      foreach (var (executablePath, argumentPrefix) in FallbackBrowsers)
      {
        if (!File.Exists(executablePath))
          continue;

        if (TryStartProcess(executablePath, $"{argumentPrefix} \"{normalizedUrl}\""))
          return true;
      }
    }

    return TryShellOpen(normalizedUrl);
  }

  private static bool TryOpenWithDefaultBrowserIncognito(string url)
  {
    var progId = GetDefaultBrowserProgId();
    if (string.IsNullOrWhiteSpace(progId))
      return false;

    var executable = GetExecutableFromProgId(progId);
    if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
      return false;

    var arguments = BuildIncognitoArguments(progId, url);
    return arguments is not null && TryStartProcess(executable, arguments);
  }

  private static string? GetDefaultBrowserProgId()
  {
    if (!OperatingSystem.IsWindows())
      return null;

    using var userChoice = Registry.CurrentUser.OpenSubKey(
      @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");

    return userChoice?.GetValue("ProgId") as string;
  }

  private static string? GetExecutableFromProgId(string progId)
  {
    using var commandKey = Registry.ClassesRoot.OpenSubKey($@"{progId}\shell\open\command");
    var command = commandKey?.GetValue(null) as string;
    if (string.IsNullOrWhiteSpace(command))
      return null;

    var match = Regex.Match(command, "^\"([^\"]+)\"");
    if (match.Success)
      return match.Groups[1].Value;

    var firstToken = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    return firstToken?.Trim('"');
  }

  private static string? BuildIncognitoArguments(string progId, string url)
  {
    var browserId = progId.ToUpperInvariant();
    var quotedUrl = $"\"{url}\"";

    if (browserId.Contains("CHROME", StringComparison.Ordinal) || browserId.Contains("BRAVE", StringComparison.Ordinal))
      return $"--incognito {quotedUrl}";

    if (browserId.Contains("EDGE", StringComparison.Ordinal) || browserId.Contains("MSEDGE", StringComparison.Ordinal))
      return $"--inprivate {quotedUrl}";

    if (browserId.Contains("FIREFOX", StringComparison.Ordinal))
      return $"-private-window {quotedUrl}";

    if (browserId.Contains("OPERA", StringComparison.Ordinal))
      return $"--private {quotedUrl}";

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
