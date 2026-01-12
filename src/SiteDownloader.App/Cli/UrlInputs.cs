namespace SiteDownloader.App.Cli;

public static class UrlInputs
{
    public static async Task<IReadOnlyList<Uri>> GetUrlsAsync(AppArguments args, CancellationToken cancellationToken)
    {
        var urls = new List<string>();

        if (args.Urls.Count > 0)
        {
            urls.AddRange(args.Urls);
        }

        if (!string.IsNullOrWhiteSpace(args.FilePath))
        {
            var fromFile = await ReadUrlsFromFileAsync(args.FilePath!, cancellationToken);
            urls.AddRange(fromFile);
        }

        if (urls.Count == 0)
        {
            urls.AddRange(await ReadUrlsInteractivelyAsync(cancellationToken));
        }

        return urls
            .Select(TryParseUri)
            .Where(u => u is not null)
            .Select(u => u!)
            .Distinct()
            .ToArray();
    }

    private static Uri? TryParseUri(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        raw = raw.Trim();

        // Convenience: allow user to type example.com
        if (!raw.Contains("://", StringComparison.Ordinal))
        {
            raw = "https://" + raw;
        }

        return Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
    }

    private static async Task<IReadOnlyList<string>> ReadUrlsFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("URL list file not found.", filePath);
        }

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        return lines
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();
    }

    private static Task<IReadOnlyList<string>> ReadUrlsInteractivelyAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Enter URLs (one per line). Submit an empty line to start downloading:");

        var lines = new List<string>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = Console.ReadLine();
            if (line is null) break;

            line = line.Trim();
            if (line.Length == 0) break;

            lines.Add(line);
        }

        return Task.FromResult<IReadOnlyList<string>>(lines);
    }
}
