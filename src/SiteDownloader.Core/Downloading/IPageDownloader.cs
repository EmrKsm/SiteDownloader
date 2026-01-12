namespace SiteDownloader;

public interface IPageDownloader
{
    Task<HttpResponseMessage> DownloadAsync(Uri url, CancellationToken cancellationToken);
}
