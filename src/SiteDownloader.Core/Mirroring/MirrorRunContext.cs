using System.Collections.Concurrent;

namespace SiteDownloader.Mirroring;

public sealed class MirrorRunContext
{
    private readonly ConcurrentDictionary<Uri, Task<DownloadedAsset?>> _downloads = new();

    public Task<DownloadedAsset?> GetOrAdd(Uri url, Func<Uri, Task<DownloadedAsset?>> factory)
        => _downloads.GetOrAdd(url, factory);
}
