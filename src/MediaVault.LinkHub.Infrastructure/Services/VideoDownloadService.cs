using System.Collections.Concurrent;
using System.Diagnostics;

using MediaVault.LinkHub.Application.Models.Downloads;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Downloads;
using MediaVault.LinkHub.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class VideoDownloadService : IVideoDownloadService, IDisposable
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VideoDownloadStorage _storage;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeDownloads = new();
    private readonly SemaphoreSlim _queueGate = new(1, 1);
    private readonly object _pumpLock = new();
    private bool _pumpRunning;
    private bool _disposed;

    public VideoDownloadService(
        IDbContextFactory<AppDbContext> contextFactory,
        IHttpClientFactory httpClientFactory,
        VideoDownloadStorage storage)
    {
        _contextFactory = contextFactory;
        _httpClientFactory = httpClientFactory;
        _storage = storage;
    }

    public event EventHandler<VideoDownloadProgressUpdate>? ProgressChanged;

    public async Task<IReadOnlyList<VideoDownload>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.VideoDownloads
            .AsNoTracking()
            .OrderByDescending(download => download.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VideoDownload> EnqueueAsync(
        string sourceUrl,
        string? displayName = null,
        CancellationToken cancellationToken = default)
    {
        var url = NormalizeUrl(sourceUrl);
        var name = string.IsNullOrWhiteSpace(displayName)
            ? DeriveDisplayName(url)
            : displayName.Trim();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = new VideoDownload
        {
            SourceUrl = url,
            DisplayName = name,
            Status = VideoDownloadStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        context.VideoDownloads.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        RaiseProgress(entity);
        EnsurePumpRunning();
        return entity;
    }

    public async Task RetryAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.VideoDownloads.FirstOrDefaultAsync(download => download.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró la descarga con Id {id}.");

        if (entity.Status is VideoDownloadStatus.Pending or VideoDownloadStatus.Downloading)
            return;

        if (!string.IsNullOrWhiteSpace(entity.LocalPath))
            _storage.TryDeleteManaged(entity.LocalPath);

        entity.Status = VideoDownloadStatus.Pending;
        entity.ProgressPercent = 0;
        entity.SegmentsTotal = 0;
        entity.SegmentsCompleted = 0;
        entity.LocalPath = null;
        entity.ErrorMessage = null;
        entity.StartedAt = null;
        entity.CompletedAt = null;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        RaiseProgress(entity);
        EnsurePumpRunning();
    }

    public async Task CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        if (_activeDownloads.TryGetValue(id, out var cts))
        {
            cts.Cancel();
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.VideoDownloads.FirstOrDefaultAsync(download => download.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null || entity.Status != VideoDownloadStatus.Pending)
            return;

        entity.Status = VideoDownloadStatus.Cancelled;
        entity.ErrorMessage = "Cancelada por el usuario.";
        entity.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        RaiseProgress(entity);
    }

    public async Task DeleteAsync(int id, bool deleteFile = true, CancellationToken cancellationToken = default)
    {
        if (_activeDownloads.TryGetValue(id, out var cts))
            cts.Cancel();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.VideoDownloads.FirstOrDefaultAsync(download => download.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return;

        if (deleteFile)
            _storage.TryDeleteManaged(entity.LocalPath);

        context.VideoDownloads.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecoverInterruptedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var interrupted = await context.VideoDownloads
            .Where(download => download.Status == VideoDownloadStatus.Downloading)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (interrupted.Count == 0)
        {
            EnsurePumpRunning();
            return;
        }

        foreach (var entity in interrupted)
        {
            entity.Status = VideoDownloadStatus.Failed;
            entity.ErrorMessage = "Descarga interrumpida al cerrar la aplicación.";
            entity.CompletedAt = DateTime.UtcNow;
            RaiseProgress(entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        EnsurePumpRunning();
    }

    public bool TryOpen(int id)
    {
        using var context = _contextFactory.CreateDbContext();
        var entity = context.VideoDownloads.AsNoTracking().FirstOrDefault(download => download.Id == id);
        if (entity is null
            || entity.Status != VideoDownloadStatus.Completed
            || string.IsNullOrWhiteSpace(entity.LocalPath)
            || !File.Exists(entity.LocalPath))
        {
            return false;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = entity.LocalPath,
                UseShellExecute = true
            });
            return process is not null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private void EnsurePumpRunning()
    {
        lock (_pumpLock)
        {
            if (_pumpRunning || _disposed)
                return;

            _pumpRunning = true;
            _ = Task.Run(PumpAsync);
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            while (!_disposed)
            {
                var nextId = await TryDequeueNextPendingIdAsync().ConfigureAwait(false);
                if (nextId is null)
                    break;

                await DownloadOneAsync(nextId.Value).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_pumpLock)
            {
                _pumpRunning = false;
            }

            // Si quedó trabajo pendiente (p. ej. Enqueue mientras terminaba), reactivar.
            if (!_disposed && HasPending())
                EnsurePumpRunning();
        }
    }

    private bool HasPending()
    {
        using var context = _contextFactory.CreateDbContext();
        return context.VideoDownloads.Any(download => download.Status == VideoDownloadStatus.Pending);
    }

    private async Task<int?> TryDequeueNextPendingIdAsync()
    {
        await _queueGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var next = await context.VideoDownloads
                .Where(download => download.Status == VideoDownloadStatus.Pending)
                .OrderBy(download => download.CreatedAt)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            if (next is null)
                return null;

            next.Status = VideoDownloadStatus.Downloading;
            next.StartedAt = DateTime.UtcNow;
            next.ProgressPercent = 0;
            next.ErrorMessage = null;
            await context.SaveChangesAsync().ConfigureAwait(false);
            RaiseProgress(next);
            return next.Id;
        }
        finally
        {
            _queueGate.Release();
        }
    }

    private async Task DownloadOneAsync(int id)
    {
        using var cts = new CancellationTokenSource();
        _activeDownloads[id] = cts;
        var cancellationToken = cts.Token;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var entity = await context.VideoDownloads.FirstOrDefaultAsync(download => download.Id == id, cancellationToken)
                .ConfigureAwait(false);
            if (entity is null)
                return;

            var outputPath = _storage.BuildOutputPath(entity.Id, entity.DisplayName);
            entity.LocalPath = outputPath;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await DownloadToFileAsync(entity.SourceUrl, outputPath, entity, context, cancellationToken)
                .ConfigureAwait(false);

            entity.Status = VideoDownloadStatus.Completed;
            entity.ProgressPercent = 100;
            entity.CompletedAt = DateTime.UtcNow;
            entity.ErrorMessage = null;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            RaiseProgress(entity);
        }
        catch (OperationCanceledException)
        {
            await MarkTerminalAsync(id, VideoDownloadStatus.Cancelled, "Cancelada por el usuario.").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await MarkTerminalAsync(id, VideoDownloadStatus.Failed, ex.Message).ConfigureAwait(false);
        }
        finally
        {
            _activeDownloads.TryRemove(id, out _);
        }
    }

    private async Task DownloadToFileAsync(
        string sourceUrl,
        string outputPath,
        VideoDownload entity,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var sourceUri))
            throw new InvalidOperationException("La URL no es válida.");

        var client = _httpClientFactory.CreateClient("HlsDownloader");
        var playlistText = await DownloadTextAsync(client, sourceUri, cancellationToken).ConfigureAwait(false);

        Uri mediaPlaylistUri = sourceUri;
        if (HlsPlaylistParser.IsMasterPlaylist(playlistText))
        {
            var variant = HlsPlaylistParser.SelectBestVariant(playlistText, sourceUri)
                ?? throw new InvalidOperationException("No se encontró una variante en el master playlist.");
            mediaPlaylistUri = variant;
            playlistText = await DownloadTextAsync(client, mediaPlaylistUri, cancellationToken).ConfigureAwait(false);
        }
        else if (!LooksLikePlaylist(playlistText) && LooksLikeMediaUrl(sourceUri))
        {
            // URL directa de media (no m3u8): descarga simple.
            await DownloadBinaryToFileAsync(client, sourceUri, outputPath, entity, context, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (HlsPlaylistParser.IsEncrypted(playlistText))
            throw new NotSupportedException("Este stream HLS está cifrado (#EXT-X-KEY) y aún no se soporta.");

        var segments = HlsPlaylistParser.ParseSegments(playlistText, mediaPlaylistUri);
        if (segments.Count == 0)
            throw new InvalidOperationException("La playlist no contiene segmentos descargables.");

        entity.SegmentsTotal = segments.Count;
        entity.SegmentsCompleted = 0;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        RaiseProgress(entity);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await using var output = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        for (var i = 0; i < segments.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AppendSegmentAsync(client, segments[i], output, cancellationToken).ConfigureAwait(false);

            entity.SegmentsCompleted = i + 1;
            entity.ProgressPercent = (int)Math.Round((i + 1) * 100.0 / segments.Count);
            if (i == segments.Count - 1 || i % 2 == 0)
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                RaiseProgress(entity);
            }
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DownloadBinaryToFileAsync(
        HttpClient client,
        Uri uri,
        string outputPath,
        VideoDownload entity,
        AppDbContext context,
        CancellationToken cancellationToken)
    {
        entity.SegmentsTotal = 1;
        entity.SegmentsCompleted = 0;
        entity.ProgressPercent = 0;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        RaiseProgress(entity);

        using var request = CreateRequest(HttpMethod.Get, uri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            outputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        entity.SegmentsCompleted = 1;
        entity.ProgressPercent = 100;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        RaiseProgress(entity);
    }

    private static async Task AppendSegmentAsync(
        HttpClient client,
        Uri segmentUri,
        Stream output,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, segmentUri);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await stream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> DownloadTextAsync(
        HttpClient client,
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, uri);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("*/*");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

        var origin = uri.GetLeftPart(UriPartial.Authority);
        if (!string.IsNullOrWhiteSpace(origin))
        {
            request.Headers.Referrer = new Uri(origin + "/");
            request.Headers.TryAddWithoutValidation("Origin", origin);
        }

        return request;
    }

    private async Task MarkTerminalAsync(int id, VideoDownloadStatus status, string message)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
            var entity = await context.VideoDownloads.FirstOrDefaultAsync(download => download.Id == id)
                .ConfigureAwait(false);
            if (entity is null)
                return;

            if (status != VideoDownloadStatus.Completed && !string.IsNullOrWhiteSpace(entity.LocalPath))
            {
                _storage.TryDeleteManaged(entity.LocalPath);
                entity.LocalPath = null;
            }

            entity.Status = status;
            entity.ErrorMessage = Truncate(message, 2000);
            entity.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync().ConfigureAwait(false);
            RaiseProgress(entity);
        }
        catch
        {
            // Evitar que un fallo al marcar estado tumbe el pump.
        }
    }

    private void RaiseProgress(VideoDownload entity)
    {
        ProgressChanged?.Invoke(this, new VideoDownloadProgressUpdate
        {
            Id = entity.Id,
            Status = entity.Status,
            ProgressPercent = entity.ProgressPercent,
            SegmentsTotal = entity.SegmentsTotal,
            SegmentsCompleted = entity.SegmentsCompleted,
            LocalPath = entity.LocalPath,
            ErrorMessage = entity.ErrorMessage,
            CompletedAt = entity.CompletedAt
        });
    }

    private static string NormalizeUrl(string sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
            throw new ArgumentException("La URL es obligatoria.", nameof(sourceUrl));

        var trimmed = sourceUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("La URL debe ser http(s) absoluta.", nameof(sourceUrl));
        }

        return trimmed;
    }

    private static string DeriveDisplayName(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return $"video-{DateTime.Now:yyyyMMdd-HHmmss}";

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            var part = parts[i];
            if (part.Contains(".m3u8", StringComparison.OrdinalIgnoreCase))
                continue;
            if (part.Contains(".ts", StringComparison.OrdinalIgnoreCase))
                continue;
            return part;
        }

        return $"video-{DateTime.Now:yyyyMMdd-HHmmss}";
    }

    private static bool LooksLikePlaylist(string content)
    {
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("#EXTINF", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("#EXT-X-STREAM-INF", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeMediaUrl(Uri uri)
    {
        var path = uri.AbsolutePath;
        return path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".m4s", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var pair in _activeDownloads)
        {
            try
            {
                pair.Value.Cancel();
                pair.Value.Dispose();
            }
            catch
            {
            }
        }

        _activeDownloads.Clear();
        _queueGate.Dispose();
    }
}
