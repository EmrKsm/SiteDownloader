namespace SiteDownloader.Downloading;

public sealed class HttpPageDownloader(HttpClient httpClient) : IPageDownloader
{
    public async Task<HttpResponseMessage> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", "SiteDownloader/1.0 (+https://example.invalid)");

        // ResponseHeadersRead ensures we don't buffer the whole body in memory.
        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }
}
