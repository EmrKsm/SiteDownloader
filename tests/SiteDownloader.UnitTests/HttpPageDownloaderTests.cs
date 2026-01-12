using System.Net;

namespace SiteDownloader.UnitTests;

public sealed class HttpPageDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_Returns_Response()
    {
        using var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Hello</html>")
        });

        using var client = new HttpClient(handler);
        var downloader = new SiteDownloader.HttpPageDownloader(client);

        using var response = await downloader.DownloadAsync(new Uri("https://example.com/"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hello", body);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
