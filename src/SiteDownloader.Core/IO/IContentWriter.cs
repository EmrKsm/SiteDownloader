namespace SiteDownloader;

public interface IContentWriter
{
    Task<string> SaveAsync(Uri url, HttpResponseMessage response, string outputRoot, CancellationToken cancellationToken);
}
