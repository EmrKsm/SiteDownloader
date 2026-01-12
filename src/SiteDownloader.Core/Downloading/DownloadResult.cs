using System.Net;

namespace SiteDownloader.Downloading;

public sealed record DownloadResult(
    Uri Url,
    bool Success,
    HttpStatusCode? StatusCode,
    string? OutputPath,
    string? Error
);
