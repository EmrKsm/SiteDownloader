using System.Net;

namespace SiteDownloader;

public sealed record DownloadResult(
    Uri Url,
    bool Success,
    HttpStatusCode? StatusCode,
    string? OutputPath,
    string? Error
);
