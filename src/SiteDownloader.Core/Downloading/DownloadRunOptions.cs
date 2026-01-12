namespace SiteDownloader;

public sealed record DownloadRunOptions(
    string OutputRoot,
    int MaxConcurrency,
    TimeSpan RequestTimeout
);
