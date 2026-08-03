using System.Diagnostics;
using System.IO;
using System.Windows.Media;

using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Genera miniaturas de video usando VLC (filtro scene).
/// </summary>
internal static class VlcVideoThumbnailProvider
{
    private const int ProcessTimeoutMs = 20000;

    public static ImageSource? TryGetThumbnail(string path, int targetSize)
    {
        if (!MediaFileExtensions.IsVideo(path) || !File.Exists(path))
            return null;

        var vlcPath = VlcPathResolver.Resolve();
        if (vlcPath is null)
            return null;

        var tempDir = Path.Combine(Path.GetTempPath(), "MediaVaultThumbnails", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var prefix = "thumb";
            var arguments =
                $"-I dummy --intf dummy --no-audio --no-video-title-show " +
                $"--video-filter=scene --scene-format=png --scene-ratio=1 --scene-prefix={prefix} " +
                $"--scene-replace --scene-path=\"{tempDir}\" " +
                $"--start-time=0.5 --run-time=1 --play-and-exit \"{Path.GetFullPath(path)}\"";

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = vlcPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

            if (process is null)
                return null;

            if (!process.WaitForExit(ProcessTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            var snapshotPath = Directory
                .EnumerateFiles(tempDir, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (snapshotPath is null)
                return null;

            return LocalImageLoader.TryLoad(snapshotPath, targetSize);
        }
        catch
        {
            return null;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
