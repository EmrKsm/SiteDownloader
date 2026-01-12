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
            .Select(_ => ProcessChannelAsync(channel.Reader, options, cancellationToken, results, resultsLock))
            .ToArray();

        async Task ProcessChannelAsync(
            ChannelReader<Uri> reader,
            DownloadRunOptions opts,
            CancellationToken ct,
            List<DownloadResult> resultsList,
            object lockObj)
        {
            await foreach (var url in reader.ReadAllAsync(ct))
            {
                var result = await ProcessOneAsync(url, opts, ct);
                lock (lockObj)
                {
                    resultsList.Add(result);
                }
            }
        }

        try
        {
            foreach (var url in urls)
            {
                await channel.Writer.WriteAsync(url, cancellationToken);
            }
        }
        finally
        {
            channel.Writer.Complete();
        }

        await Task.WhenAll(workers);
        return results;
    }

    private async Task<DownloadResult> ProcessOneAsync(Uri url, DownloadRunOptions options, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.RequestTimeout);

        try
        {
            logger.LogInformation("Downloading {Url}", url);

            using var response = await downloader.DownloadAsync(url, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode;
                logger.LogWarning("Failed {Url} with status {StatusCode}", url, (int)code);

                // Still save body if any? For this exercise, skip saving on non-success.
                return new DownloadResult(url, Success: false, StatusCode: code, OutputPath: null, Error: $"HTTP {(int)code} {code}");
            }

            var outputPath = await writer.SaveAsync(url, response, options.OutputRoot, timeoutCts.Token);
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
