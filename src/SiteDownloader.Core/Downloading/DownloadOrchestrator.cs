using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SiteDownloader.IO;

namespace SiteDownloader.Downloading;

public sealed class DownloadOrchestrator(
    IPageDownloader downloader,
    IContentWriter writer,
    ILogger<DownloadOrchestrator> logger)
{
    public async Task<IReadOnlyList<DownloadResult>> RunAsync(
        IEnumerable<Uri> urls,
        DownloadRunOptions options,
        CancellationToken cancellationToken)
    {
        if (options.MaxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaxConcurrency), "MaxConcurrency must be > 0.");
        }

        Directory.CreateDirectory(options.OutputRoot);

        var channel = Channel.CreateUnbounded<Uri>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false
        });

        var results = new List<DownloadResult>();
        var resultsLock = new object();

        var workers = Enumerable.Range(0, options.MaxConcurrency)
            .Select(_ => Task.Run(async () =>
            {
                while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var url))
                    {
                        var result = await ProcessOneAsync(url, options, cancellationToken).ConfigureAwait(false);
                        lock (resultsLock)
                        {
                            results.Add(result);
                        }
                    }
                }
            }, cancellationToken))
            .ToArray();

        try
        {
            foreach (var url in urls)
            {
                await channel.Writer.WriteAsync(url, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            channel.Writer.TryComplete();
        }

        await Task.WhenAll(workers).ConfigureAwait(false);
        return results;
    }

    private async Task<DownloadResult> ProcessOneAsync(Uri url, DownloadRunOptions options, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.RequestTimeout);

        try
        {
            logger.LogInformation("Downloading {Url}", url);

            using var response = await downloader.DownloadAsync(url, timeoutCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode;
                logger.LogWarning("Failed {Url} with status {StatusCode}", url, (int)code);

                // Still save body if any? For this exercise, skip saving on non-success.
                return new DownloadResult(url, Success: false, StatusCode: code, OutputPath: null, Error: $"HTTP {(int)code} {code}");
            }

            var outputPath = await writer.SaveAsync(url, response, options.OutputRoot, timeoutCts.Token).ConfigureAwait(false);
            logger.LogInformation("Saved {Url} to {OutputPath}", url, outputPath);

            return new DownloadResult(url, Success: true, StatusCode: response.StatusCode, OutputPath: outputPath, Error: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Canceled {Url}", url);
            return new DownloadResult(url, Success: false, StatusCode: null, OutputPath: null, Error: "Canceled");
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Timeout {Url} after {Timeout}", url, options.RequestTimeout);
            return new DownloadResult(url, Success: false, StatusCode: null, OutputPath: null, Error: $"Timeout after {options.RequestTimeout.TotalSeconds:0}s");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Request failed for {Url}", url);
            return new DownloadResult(url, Success: false, StatusCode: null, OutputPath: null, Error: ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error for {Url}", url);
            return new DownloadResult(url, Success: false, StatusCode: null, OutputPath: null, Error: ex.Message);
        }
    }
}
