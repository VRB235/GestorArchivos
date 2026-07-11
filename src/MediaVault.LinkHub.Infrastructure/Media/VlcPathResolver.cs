namespace MediaVault.LinkHub.Infrastructure.Media;

/// <summary>
/// Localiza la instalación de VLC en el sistema.
/// </summary>
public static class VlcPathResolver
{
    private static readonly string[] DefaultCandidates =
    [
        @"C:\Program Files\VideoLAN\VLC\vlc.exe",
        @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
    ];

    public static string? Resolve()
    {
        foreach (var candidate in DefaultCandidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (var folder in pathVariable.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var vlcPath = Path.Combine(folder, "vlc.exe");
            if (File.Exists(vlcPath))
                return vlcPath;
        }

        return null;
    }
}
