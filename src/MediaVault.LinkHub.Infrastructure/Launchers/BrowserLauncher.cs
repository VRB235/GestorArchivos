using System.Diagnostics;
using MediaVault.LinkHub.Application.Services;
using Microsoft.Win32;

namespace MediaVault.LinkHub.Infrastructure.Launchers;

/// <summary>
/// Abre URLs en Firefox (ventana normal) en Windows.
/// Soporta instalación clásica (Program Files / registro) y Microsoft Store (App Execution Alias).
/// </summary>
public sealed class BrowserLauncher : IBrowserLauncher
{
    private static readonly string[] FirefoxCandidates =
    [
        @"C:\Program Files\Mozilla Firefox\firefox.exe",
        @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
    ];

    public bool TryOpenInFirefox(string url)
    {
        if (!TryNormalizeUrl(url, out var normalizedUrl))
            return false;

        if (!OperatingSystem.IsWindows())
            return false;

        var arguments = $"\"{normalizedUrl}\"";

        foreach (var executablePath in EnumerateFirefoxExecutables())
        {
            if (IsAppExecutionAlias(executablePath))
            {
                // Los stubs de WindowsApps (0 bytes / reparse) solo funcionan con shell execute.
                // Con UseShellExecute=false suelen devolver un Process que sale al instante con código 0
                // sin abrir Firefox — un falso positivo.
                if (TryStartProcess(executablePath, arguments, useShellExecute: true))
                    return true;

                continue;
            }

            if (TryStartProcess(executablePath, arguments, useShellExecute: false))
                return true;

            if (TryStartProcess(executablePath, arguments, useShellExecute: true))
                return true;
        }

        // Último recurso: que el shell resuelva "firefox" vía PATH / App Execution Alias.
        return TryStartProcess("firefox", arguments, useShellExecute: true);
    }

    private static IEnumerable<string> EnumerateFirefoxExecutables()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in FirefoxCandidates)
        {
            if (File.Exists(candidate) && seen.Add(candidate))
                yield return candidate;
        }

        foreach (var fromRegistry in EnumerateFirefoxFromRegistry())
        {
            if (File.Exists(fromRegistry) && seen.Add(fromRegistry))
                yield return fromRegistry;
        }

        foreach (var fromStore in EnumerateFirefoxFromWindowsApps())
        {
            if (File.Exists(fromStore) && seen.Add(fromStore))
                yield return fromStore;
        }

        foreach (var fromPath in EnumerateFirefoxFromPath())
        {
            if (File.Exists(fromPath) && seen.Add(fromPath))
                yield return fromPath;
        }
    }

    private static IEnumerable<string> EnumerateFirefoxFromRegistry()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        RegistryKey[] hives = [Registry.LocalMachine, Registry.CurrentUser];
        string[] keys =
        [
            @"SOFTWARE\Mozilla\Mozilla Firefox",
            @"SOFTWARE\WOW6432Node\Mozilla\Mozilla Firefox"
        ];

        foreach (var hive in hives)
        {
            foreach (var keyPath in keys)
            {
                using var key = hive.OpenSubKey(keyPath);
                if (key is null)
                    continue;

                var currentVersion = key.GetValue("CurrentVersion") as string;
                if (string.IsNullOrWhiteSpace(currentVersion))
                    continue;

                using var mainKey = key.OpenSubKey($@"{currentVersion}\Main");
                var pathToExe = mainKey?.GetValue("PathToExe") as string;
                if (!string.IsNullOrWhiteSpace(pathToExe))
                    yield return pathToExe;
            }
        }
    }

    private static IEnumerable<string> EnumerateFirefoxFromWindowsApps()
    {
        var windowsApps = GetLocalWindowsAppsDirectory();
        if (!Directory.Exists(windowsApps))
            yield break;

        // Preferir el alias de ejecución (el que activa el paquete Store).
        var alias = Path.Combine(windowsApps, "firefox.exe");
        if (File.Exists(alias))
            yield return alias;
    }

    private static IEnumerable<string> EnumerateFirefoxFromPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            yield break;

        var windowsApps = GetLocalWindowsAppsDirectory();

        foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Evitar duplicar el alias de WindowsApps ya enumerado arriba.
            if (!string.IsNullOrWhiteSpace(windowsApps)
                && segment.Equals(windowsApps, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string candidate;
            try
            {
                candidate = Path.Combine(segment, "firefox.exe");
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static string GetLocalWindowsAppsDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps");

    /// <summary>
    /// Detecta stubs de App Execution Alias bajo %LocalAppData%\Microsoft\WindowsApps.
    /// </summary>
    private static bool IsAppExecutionAlias(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var windowsApps = Path.GetFullPath(GetLocalWindowsAppsDirectory())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (fullPath.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase))
                return true;

            // Stub típico: 0 bytes aunque no esté bajo WindowsApps.
            var info = new FileInfo(fullPath);
            return info.Exists && info.Length == 0;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    private static bool TryNormalizeUrl(string url, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
            return false;

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

    private static bool TryStartProcess(string fileName, string arguments, bool useShellExecute)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = useShellExecute,
                CreateNoWindow = !useShellExecute
            });

            if (process is null)
                return false;

            try
            {
                // Alias Store / stubs: a veces CreateProcess “tiene éxito” y el proceso sale al instante
                // sin haber abierto la UI. Tratarlo como fallo para probar el siguiente método.
                if (!useShellExecute && process.WaitForExit(milliseconds: 600))
                    return false;

                return true;
            }
            finally
            {
                process.Dispose();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                       or System.ComponentModel.Win32Exception
                                       or FileNotFoundException
                                       or DirectoryNotFoundException)
        {
            return false;
        }
    }
}
