using System.Security.Cryptography;
using System.Text;

namespace SiteDownloader;

public static class UrlOutputPath
{
    public static string GetOutputFilePath(string outputRoot, Uri url, string? contentType)
    {
        var host = string.IsNullOrWhiteSpace(url.Host) ? "unknown-host" : url.Host;

        var path = url.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var hasTrailingSlash = path.EndsWith("/", StringComparison.Ordinal);
        var lastSegment = segments.Length > 0 ? segments[^1] : string.Empty;

        string relativeDir;
        string fileName;

        if (segments.Length == 0)
        {
            relativeDir = string.Empty;
            fileName = "index";
        }
        else if (hasTrailingSlash)
        {
            relativeDir = string.Join(Path.DirectorySeparatorChar, segments.Select(SanitizeSegment));
            fileName = "index";
        }
        else
        {
            var last = SanitizeSegment(lastSegment);
            var dirSegments = segments.Length > 1
                ? segments.Take(segments.Length - 1).Select(SanitizeSegment)
                : Enumerable.Empty<string>();

            // If it looks like a file name, keep it; otherwise treat it as a folder with index.
            if (Path.HasExtension(last))
            {
                relativeDir = string.Join(Path.DirectorySeparatorChar, dirSegments);
                fileName = Path.GetFileNameWithoutExtension(last);
            }
            else
            {
                relativeDir = string.Join(Path.DirectorySeparatorChar, dirSegments.Append(last));
                fileName = "index";
            }
        }

        var ext = ResolveExtension(url, contentType);
        if (!string.IsNullOrWhiteSpace(url.Query))
        {
            fileName = $"{fileName}__{ShortHash(url.Query)}";
        }

        var domainDir = Path.Combine(outputRoot, host);
        var finalDir = string.IsNullOrWhiteSpace(relativeDir) ? domainDir : Path.Combine(domainDir, relativeDir);

        return Path.Combine(finalDir, fileName + ext);
    }

    private static string ResolveExtension(Uri url, string? contentType)
    {
        if (Path.HasExtension(url.AbsolutePath))
        {
            var ext = Path.GetExtension(url.AbsolutePath);
            return string.IsNullOrWhiteSpace(ext) ? ".html" : ext;
        }

        if (contentType is null)
        {
            return ".html";
        }

        var ct = contentType.Split(';', 2)[0].Trim().ToLowerInvariant();
        return ct switch
        {
            "text/html" => ".html",
            "application/json" => ".json",
            "application/xml" => ".xml",
            "text/xml" => ".xml",
            "text/plain" => ".txt",
            _ => ".bin"
        };
    }

    private static string SanitizeSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "_";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(segment.Length);
        foreach (var ch in segment)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }

        var cleaned = sb.ToString();
        return string.IsNullOrWhiteSpace(cleaned) ? "_" : cleaned;
    }

    private static string ShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        // 8 bytes => 16 hex chars, readable but low collision risk for query strings.
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
