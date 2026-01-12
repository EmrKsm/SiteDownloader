namespace SiteDownloader.Mirroring;

public sealed record DownloadedAsset(
    Uri Url,
    string OutputPath,
    string? ContentType
);
