namespace SiteDownloader;

public enum LogFormat
{
    Json,
    Text
}

public sealed record AppArguments(
    IReadOnlyList<string> Urls,
    string? FilePath,
    int MaxConcurrency,
    int TimeoutSeconds,
    string OutputRoot,
    LogFormat LogFormat,
    bool DownloadAssets,
    bool IncludeThirdPartyAssets,
    bool ShowHelp
)
{
    public static AppArguments Default { get; } = new(
        Urls: Array.Empty<string>(),
        FilePath: null,
        MaxConcurrency: 8,
        TimeoutSeconds: 30,
        OutputRoot: "downloads",
        LogFormat: LogFormat.Json,
        DownloadAssets: false,
        IncludeThirdPartyAssets: false,
        ShowHelp: false);

    public static bool TryParse(string[] args, out AppArguments parsed, out string? error)
    {
        parsed = Default;
        error = null;

        var urls = new List<string>();
        string? file = null;
        var maxConcurrency = parsed.MaxConcurrency;
        var timeoutSeconds = parsed.TimeoutSeconds;
        var outputRoot = parsed.OutputRoot;
        var logFormat = parsed.LogFormat;
        var downloadAssets = parsed.DownloadAssets;
        var includeThirdPartyAssets = parsed.IncludeThirdPartyAssets;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "-h" or "--help" or "/?")
            {
                showHelp = true;
                continue;
            }

            if (a is "--url")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                urls.Add(v);
                continue;
            }

            if (a is "--urls")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                urls.AddRange(v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                continue;
            }

            if (a is "--file")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                file = v;
                continue;
            }

            if (a is "--max-concurrency")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                if (!int.TryParse(v, out maxConcurrency) || maxConcurrency <= 0)
                {
                    error = "--max-concurrency must be a positive integer.";
                    return false;
                }
                continue;
            }

            if (a is "--timeout-seconds")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                if (!int.TryParse(v, out timeoutSeconds) || timeoutSeconds <= 0)
                {
                    error = "--timeout-seconds must be a positive integer.";
                    return false;
                }
                continue;
            }

            if (a is "--output")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                outputRoot = v;
                continue;
            }

            if (a is "--log-format")
            {
                if (!TryGetValue(args, ref i, out var v, out error)) return false;
                var normalized = v.ToLowerInvariant();
                if (normalized is "json")
                {
                    logFormat = LogFormat.Json;
                }
                else if (normalized is "text")
                {
                    logFormat = LogFormat.Text;
                }
                else
                {
                    error = "--log-format must be 'json' or 'text'.";
                    return false;
                }
                continue;
            }

            if (a is "--download-assets")
            {
                downloadAssets = true;
                continue;
            }

            if (a is "--include-third-party-assets")
            {
                includeThirdPartyAssets = true;
                continue;
            }

            error = $"Unknown argument: {a}";
            return false;
        }

        parsed = new AppArguments(urls, file, maxConcurrency, timeoutSeconds, outputRoot, logFormat, downloadAssets, includeThirdPartyAssets, showHelp);
        return true;
    }

    private static bool TryGetValue(string[] args, ref int index, out string value, out string? error)
    {
        error = null;
        value = string.Empty;

        if (index + 1 >= args.Length)
        {
            error = $"Missing value after {args[index]}";
            return false;
        }

        index++;
        value = args[index];
        return true;
    }

    public static string HelpText =>
        "SiteDownloader - async multi-page downloader\n\n" +
        "Usage:\n" +
        "  SiteDownloader.App --url <url> [--url <url> ...]\n" +
        "  SiteDownloader.App --urls \"<url1>,<url2>,...\"\n" +
        "  SiteDownloader.App --file <path-to-txt>\n\n" +
        "Options:\n" +
        "  --max-concurrency <N>     Default: 8\n" +
        "  --timeout-seconds <N>     Default: 30\n" +
        "  --output <path>           Default: ./downloads\n" +
        "  --log-format json|text    Default: json\n" +
        "  --download-assets         Also download referenced assets (CSS/JS/img) and rewrite HTML for offline viewing\n" +
        "  --include-third-party-assets  Allow assets from other domains (default: same-origin only)\n" +
        "  -h|--help                 Show help\n";
}
