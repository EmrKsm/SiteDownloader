namespace SiteDownloader.Downloading;

public sealed record DownloadRunOptions(
    string OutputRoot,
    int MaxConcurrency,
    TimeSpan RequestTimeout
);
