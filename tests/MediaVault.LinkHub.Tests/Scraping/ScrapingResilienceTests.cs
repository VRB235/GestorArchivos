using System.Net;
using System.Net.Http;
using System.Text;

using FluentAssertions;

using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Infrastructure.Scraping;

using Xunit;

namespace MediaVault.LinkHub.Tests.Scraping;

public sealed class TransientHttpRetryHandlerTests
{
    [Fact]
    public async Task Retries_transient_5xx_then_succeeds()
    {
        var attempts = 0;
        var inner = new StubHandler(_ =>
        {
            attempts++;
            if (attempts < 3)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>ok</html>", Encoding.UTF8, "text/html")
            };
        });

        using var handler = new TransientHttpRetryHandler(inner, maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(1));
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.test/page");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        attempts.Should().Be(3);
    }
}

public sealed class VideoScraperSessionTests
{
    [Fact]
    public void MergeCookies_adds_cookies_for_uri()
    {
        var session = new VideoScraperSession();
        var uri = new Uri("https://www.example.com/models/x");

        session.MergeCookies(
        [
            new BrowserCookie
            {
                Name = "session",
                Value = "abc",
                Domain = "www.example.com",
                Path = "/",
                Secure = true
            }
        ], uri);

        session.CountCookies(uri).Should().BeGreaterThan(0);
        session.CookieContainer.GetCookies(uri)["session"]!.Value.Should().Be("abc");
    }
}

internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
        _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(_responder(request));
}
