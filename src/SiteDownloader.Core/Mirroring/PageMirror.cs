using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace SiteDownloader.Mirroring;

public sealed class PageMirror(
    IPageDownloader downloader,
    IContentWriter writer,
    ILogger<PageMirror> logger) : IPageMirror
{
    private static readonly Regex CssUrlRegex = new(
        // url(foo) or url('foo') or url("foo")
        "url\\(\\s*(?:'(?<u>[^']*)'|\"(?<u>[^\"]*)\"|(?<u>[^)]*))\\s*\\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<string> SaveHtmlWithAssetsAsync(
        Uri pageUrl,
        HttpResponseMessage response,
        DownloadRunOptions options,
        MirrorRunContext context,
        CancellationToken cancellationToken)
    {
        var htmlOutputPath = UrlOutputPath.GetOutputFilePath(options.OutputRoot, pageUrl, "text/html");
        Directory.CreateDirectory(Path.GetDirectoryName(htmlOutputPath)!);

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);

        var references = ExtractAssetReferences(document);
        if (references.Count == 0)
        {
            await File.WriteAllTextAsync(htmlOutputPath, html, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
                .ConfigureAwait(false);
            return htmlOutputPath;
        }

        // Download assets (deduped across pages by MirrorRunContext)
        var assets = await DownloadAssetsAsync(pageUrl, references, options, context, cancellationToken).ConfigureAwait(false);

        // Rewrite DOM references to local files
        var htmlDir = Path.GetDirectoryName(htmlOutputPath)!;
        foreach (var r in references)
        {
            if (!TryResolveAssetUri(pageUrl, r.RawValue, out var assetUri))
            {
                continue;
            }

            if (assetUri is null)
            {
                continue;
            }

            if (!assets.TryGetValue(assetUri, out var downloaded) || downloaded is null)
            {
                continue;
            }

            var replacement = ToRelativeWebPath(htmlDir, downloaded.OutputPath);
            r.ApplyReplacement(replacement);
        }

        // Serialize document
        var finalHtml = document.DocumentElement?.OuterHtml ?? html;
        await File.WriteAllTextAsync(htmlOutputPath, finalHtml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Mirrored {Url} with {AssetCount} assets", pageUrl, assets.Count);
        return htmlOutputPath;
    }

    private async Task<Dictionary<Uri, DownloadedAsset?>> DownloadAssetsAsync(
        Uri pageUrl,
        List<AssetReference> references,
        DownloadRunOptions options,
        MirrorRunContext context,
        CancellationToken cancellationToken)
    {
        var uris = references
            .Select(r => r.RawValue)
            .Select(raw => TryResolveAssetUri(pageUrl, raw, out var u) ? u : null)
            .Where(u => u is not null)
            .Select(u => u!)
            .Where(u => IsDownloadable(u, pageUrl, options.IncludeThirdPartyAssets))
            .Distinct()
            .ToArray();

        var results = new Dictionary<Uri, DownloadedAsset?>();

        // First pass: download referenced assets
        await ProcessWithChannelAsync(
            uris,
            options.MaxConcurrency,
            async assetUri =>
            {
                var downloaded = await DownloadAndSaveAsync(assetUri, options, context, cancellationToken).ConfigureAwait(false);
                lock (results)
                {
                    results[assetUri] = downloaded;
                }

                // If it's CSS, parse and fetch url(...) dependencies and rewrite the CSS file.
                if (downloaded is not null && IsCss(downloaded))
                {
                    await MirrorCssDependenciesAsync(assetUri, downloaded.OutputPath, options, context, pageUrl, cancellationToken)
                        .ConfigureAwait(false);
                }
            },
            cancellationToken).ConfigureAwait(false);

        return results;
    }

    private async Task MirrorCssDependenciesAsync(
        Uri cssUri,
        string cssOutputPath,
        DownloadRunOptions options,
        MirrorRunContext context,
        Uri pageUrl,
        CancellationToken cancellationToken)
    {
        string css;
        try
        {
            css = await File.ReadAllTextAsync(cssOutputPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read css {CssPath}", cssOutputPath);
            return;
        }

        var matches = CssUrlRegex.Matches(css);
        if (matches.Count == 0)
        {
            return;
        }

        var cssDir = Path.GetDirectoryName(cssOutputPath)!;

        // Collect unique dependency URLs
        var deps = matches
            .Select(m => m.Groups["u"].Value.Trim())
            .Where(v => v.Length > 0)
            .Where(v => !v.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .Select(v => TryResolveAssetUri(cssUri, v, out var dep) ? dep : null)
            .Where(u => u is not null)
            .Select(u => u!)
            .Where(u => IsDownloadable(u, pageUrl, options.IncludeThirdPartyAssets))
            .Distinct()
            .ToArray();

        if (deps.Length == 0)
        {
            return;
        }

        var map = new Dictionary<Uri, DownloadedAsset?>();

        await ProcessWithChannelAsync(
            deps,
            options.MaxConcurrency,
            async depUri =>
            {
                var downloaded = await DownloadAndSaveAsync(depUri, options, context, cancellationToken).ConfigureAwait(false);
                lock (map)
                {
                    map[depUri] = downloaded;
                }
            },
            cancellationToken).ConfigureAwait(false);

        // Rewrite CSS content
        var rewritten = CssUrlRegex.Replace(css, match =>
        {
            var raw = match.Groups["u"].Value.Trim();
            if (!TryResolveAssetUri(cssUri, raw, out var resolved))
            {
                return match.Value;
            }

            if (resolved is null)
            {
                return match.Value;
            }

            if (!map.TryGetValue(resolved, out var downloaded) || downloaded is null)
            {
                return match.Value;
            }

            var rel = ToRelativeWebPath(cssDir, downloaded.OutputPath);
            return $"url({rel})";
        });

        await File.WriteAllTextAsync(cssOutputPath, rewritten, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
    }

    private async Task<DownloadedAsset?> DownloadAndSaveAsync(
        Uri assetUri,
        DownloadRunOptions options,
        MirrorRunContext context,
        CancellationToken cancellationToken)
    {
        // Deduplicate across the whole run.
        return await context.GetOrAdd(assetUri, async uri =>
        {
            try
            {
                using var response = await downloader.DownloadAsync(uri, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Asset fetch failed {Url} status={Status}", uri, (int)response.StatusCode);
                    return null;
                }

                var outputPath = await writer.SaveAsync(uri, response, options.OutputRoot, cancellationToken).ConfigureAwait(false);
                var contentType = response.Content.Headers.ContentType?.ToString();
                return new DownloadedAsset(uri, outputPath, contentType);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Asset fetch failed {Url}", uri);
                return null;
            }
        }).ConfigureAwait(false);
    }

    private static bool IsCss(DownloadedAsset downloaded)
    {
        if (downloaded.ContentType is not null && downloaded.ContentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return downloaded.OutputPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDownloadable(Uri candidate, Uri pageUrl, bool includeThirdParty)
    {
        if (candidate.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (!includeThirdParty)
        {
            return SameOrigin(candidate, pageUrl);
        }

        return true;
    }

    private static bool SameOrigin(Uri a, Uri b)
    {
        var aPort = a.IsDefaultPort ? (a.Scheme == "https" ? 443 : 80) : a.Port;
        var bPort = b.IsDefaultPort ? (b.Scheme == "https" ? 443 : 80) : b.Port;

        return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase)
               && aPort == bPort;
    }

    private static bool TryResolveAssetUri(Uri baseUri, string raw, out Uri? assetUri)
    {
        assetUri = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        raw = raw.Trim();

        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (raw.StartsWith("//", StringComparison.Ordinal))
        {
            assetUri = new Uri($"{baseUri.Scheme}:{raw}");
            return true;
        }

        return Uri.TryCreate(baseUri, raw, out assetUri);
    }

    private static string ToRelativeWebPath(string fromDirectory, string toFilePath)
    {
        var rel = Path.GetRelativePath(fromDirectory, toFilePath);
        // For browser-friendly relative links
        return rel.Replace('\\', '/');
    }

    private static List<AssetReference> ExtractAssetReferences(IDocument document)
    {
        var refs = new List<AssetReference>();

        // img/src
        foreach (var el in document.QuerySelectorAll("img[src]"))
        {
            refs.Add(AssetReference.ForAttribute(el, "src"));
        }

        // script/src
        foreach (var el in document.QuerySelectorAll("script[src]"))
        {
            refs.Add(AssetReference.ForAttribute(el, "src"));
        }

        // link/href (stylesheets + icons etc)
        foreach (var el in document.QuerySelectorAll("link[href]"))
        {
            refs.Add(AssetReference.ForAttribute(el, "href"));
        }

        // source/src
        foreach (var el in document.QuerySelectorAll("source[src]"))
        {
            refs.Add(AssetReference.ForAttribute(el, "src"));
        }

        // video/audio src
        foreach (var el in document.QuerySelectorAll("video[src],audio[src]"))
        {
            refs.Add(AssetReference.ForAttribute(el, "src"));
        }

        // srcset
        foreach (var el in document.QuerySelectorAll("img[srcset],source[srcset]"))
        {
            refs.Add(AssetReference.ForSrcSet(el, "srcset"));
        }

        // Remove obviously useless entries
        refs.RemoveAll(r => string.IsNullOrWhiteSpace(r.RawValue));
        return refs;
    }

    private static async Task ProcessWithChannelAsync<T>(
        IReadOnlyList<T> items,
        int maxConcurrency,
        Func<T, Task> handler,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return;
        }

        var channel = System.Threading.Channels.Channel.CreateUnbounded<T>(new System.Threading.Channels.UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false
        });

        var workers = Enumerable.Range(0, maxConcurrency)
            .Select(_ => Task.Run(async () =>
            {
                while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var item))
                    {
                        await handler(item).ConfigureAwait(false);
                    }
                }
            }, cancellationToken))
            .ToArray();

        try
        {
            foreach (var i in items)
            {
                await channel.Writer.WriteAsync(i, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
    }

    private sealed class AssetReference
    {
        private readonly IElement _element;
        private readonly string _attributeName;
        private readonly bool _isSrcSet;

        private AssetReference(IElement element, string attributeName, bool isSrcSet)
        {
            _element = element;
            _attributeName = attributeName;
            _isSrcSet = isSrcSet;
        }

        public string RawValue => _element.GetAttribute(_attributeName) ?? string.Empty;

        public void ApplyReplacement(string replacement)
        {
            if (!_isSrcSet)
            {
                _element.SetAttribute(_attributeName, replacement);
                return;
            }

            // Rewrite srcset candidates while preserving descriptors.
            var current = RawValue;
            var parts = current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var rewritten = parts
                .Select(p =>
                {
                    var seg = p.Trim();
                    if (seg.Length == 0) return seg;

                    var tokens = seg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (tokens.Length == 0) return seg;

                    // Replace only the URL token
                    tokens[0] = replacement;
                    return string.Join(' ', tokens);
                });

            _element.SetAttribute(_attributeName, string.Join(", ", rewritten));
        }

        public static AssetReference ForAttribute(IElement element, string attributeName)
            => new(element, attributeName, isSrcSet: false);

        public static AssetReference ForSrcSet(IElement element, string attributeName)
            => new(element, attributeName, isSrcSet: true);
    }
}
