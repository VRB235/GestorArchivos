using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Extrae un fotograma de video usando MediaPlayer de WPF (fiable para MP4/H.264).
/// </summary>
internal static class WpfVideoFrameThumbnailProvider
{
    private const int CaptureTimeoutMs = 12000;
    private const int MaxCaptureAttempts = 30;

    private static readonly SemaphoreSlim UiCaptureGate = new(1, 1);

    public static async Task<ImageSource?> TryGetThumbnailAsync(string path, int targetSize)
    {
        if (!MediaFileExtensions.IsVideo(path) || !File.Exists(path))
            return null;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return null;

        await UiCaptureGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (dispatcher.CheckAccess())
                return await CaptureAsync(path, targetSize, dispatcher).ConfigureAwait(true);

            return await dispatcher
                .InvokeAsync(() => CaptureAsync(path, targetSize, dispatcher))
                .Task
                .Unwrap()
                .ConfigureAwait(true);
        }
        finally
        {
            UiCaptureGate.Release();
        }
    }

    private static async Task<ImageSource?> CaptureAsync(string path, int targetSize, Dispatcher dispatcher)
    {
        var tcs = new TaskCompletionSource<ImageSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        MediaPlayer? player = null;
        DispatcherTimer? timer = null;
        var completed = 0;

        void Complete(ImageSource? image)
        {
            if (Interlocked.Exchange(ref completed, 1) != 0)
                return;

            timer?.Stop();
            try { player?.Close(); } catch { }
            tcs.TrySetResult(image);
        }

        try
        {
            player = new MediaPlayer
            {
                ScrubbingEnabled = true,
                Volume = 0
            };

            player.MediaFailed += (_, _) => Complete(null);

            player.MediaOpened += (_, _) =>
            {
                var seekTo = player.NaturalDuration.HasTimeSpan
                             && player.NaturalDuration.TimeSpan > TimeSpan.FromMilliseconds(500)
                    ? TimeSpan.FromMilliseconds(500)
                    : TimeSpan.Zero;

                player.Position = seekTo;
                player.Play();

                timer = new DispatcherTimer(DispatcherPriority.Render, dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(120)
                };

                var attempts = 0;
                timer.Tick += (_, _) =>
                {
                    attempts++;

                    if (player.NaturalVideoWidth <= 0 || player.NaturalVideoHeight <= 0)
                    {
                        if (attempts < MaxCaptureAttempts)
                            return;

                        Complete(null);
                        return;
                    }

                    try
                    {
                        Complete(RenderPlayerFrame(player, targetSize));
                    }
                    catch
                    {
                        Complete(null);
                    }
                };

                timer.Start();
            };

            player.Open(new Uri(Path.GetFullPath(path), UriKind.Absolute));

            _ = Task.Delay(CaptureTimeoutMs).ContinueWith(_ => Complete(null));

            return await tcs.Task.ConfigureAwait(true);
        }
        catch
        {
            Complete(null);
            return null;
        }
    }

    private static ImageSource RenderPlayerFrame(MediaPlayer player, int targetSize)
    {
        var width = player.NaturalVideoWidth;
        var height = player.NaturalVideoHeight;

        var drawing = new VideoDrawing
        {
            Player = player,
            Rect = new Rect(0, 0, width, height)
        };

        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
            context.DrawDrawing(drawing);

        var scale = Math.Min((double)targetSize / width, (double)targetSize / height);
        var renderWidth = Math.Max(1, (int)Math.Round(width * scale));
        var renderHeight = Math.Max(1, (int)Math.Round(height * scale));

        var bitmap = new RenderTargetBitmap(
            renderWidth,
            renderHeight,
            96,
            96,
            PixelFormats.Pbgra32);

        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
