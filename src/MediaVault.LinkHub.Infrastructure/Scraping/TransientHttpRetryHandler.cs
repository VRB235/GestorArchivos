using System.Net;

namespace MediaVault.LinkHub.Infrastructure.Scraping;

/// <summary>
/// Reintentos con jitter ante 408/429/5xx y fallos de red/timeouts transitorios.
/// </summary>
public sealed class TransientHttpRetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    public TransientHttpRetryHandler(HttpMessageHandler innerHandler, int maxRetries = 3, TimeSpan? baseDelay = null)
        : base(innerHandler)
    {
        _maxRetries = Math.Clamp(maxRetries, 0, 5);
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(400);
    }

    public TransientHttpRetryHandler(int maxRetries = 3, TimeSpan? baseDelay = null)
    {
        _maxRetries = Math.Clamp(maxRetries, 0, 5);
        _baseDelay = baseDelay ?? TimeSpan.FromMilliseconds(400);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        HttpRequestMessage? current = request;

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            if (attempt > 0)
                current = await CloneRequestAsync(request).ConfigureAwait(false);

            try
            {
                var response = await base.SendAsync(current, cancellationToken).ConfigureAwait(false);

                if (attempt < _maxRetries && IsTransient(response.StatusCode))
                {
                    var status = response.StatusCode;
                    response.Dispose();
                    if (attempt > 0)
                        current.Dispose();

                    await Task.Delay(ComputeDelay(attempt, status), cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                return response;
            }
            catch (Exception ex) when (IsTransientException(ex) && attempt < _maxRetries)
            {
                lastException = ex;
                if (attempt > 0)
                    current.Dispose();

                await Task.Delay(ComputeDelay(attempt, null), cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastException
            ?? new HttpRequestException("La petición HTTP falló tras varios reintentos.");
    }

    private TimeSpan ComputeDelay(int attempt, HttpStatusCode? statusCode)
    {
        var factor = statusCode == HttpStatusCode.TooManyRequests ? 2.5 : 1.0;
        var exponential = Math.Pow(2, attempt) * _baseDelay.TotalMilliseconds * factor;
        var jitter = Random.Shared.Next(50, 250);
        return TimeSpan.FromMilliseconds(Math.Min(exponential + jitter, 8_000));
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static bool IsTransientException(Exception ex) =>
        ex is HttpRequestException
        || ex is TaskCanceledException { InnerException: TimeoutException }
        || ex is IOException;

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is null)
            return clone;

        var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        clone.Content = new ByteArrayContent(bytes);
        foreach (var header in request.Content.Headers)
            clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
