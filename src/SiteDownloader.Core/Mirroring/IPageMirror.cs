using System.Net.Http;

namespace SiteDownloader.Mirroring;

public interface IPageMirror
{
    Task<string> SaveHtmlWithAssetsAsync(
        Uri pageUrl,
        HttpResponseMessage response,
        DownloadRunOptions options,
        MirrorRunContext context,
        CancellationToken cancellationToken);
}
