using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SiteDownloader;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().CreateLogger();

        if (!AppArguments.TryParse(args, out var parsed, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine();
            Console.Error.WriteLine(AppArguments.HelpText);
            return 2;
        }

        if (parsed.ShowHelp)
        {
            Console.WriteLine(AppArguments.HelpText);
            return 0;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var urls = await UrlInputs.GetUrlsAsync(parsed, cts.Token).ConfigureAwait(false);
        if (urls.Count == 0)
        {
            Console.Error.WriteLine("No valid URLs provided.");
            return 2;
        }

        var app = CreateHost(parsed).Build();

        var orchestrator = app.Services.GetRequiredService<DownloadOrchestrator>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SiteDownloader.App");

        var options = new DownloadRunOptions(
            OutputRoot: Path.GetFullPath(parsed.OutputRoot),
            MaxConcurrency: parsed.MaxConcurrency,
            RequestTimeout: TimeSpan.FromSeconds(parsed.TimeoutSeconds));

        logger.LogInformation("Starting download: {Count} urls, maxConcurrency={MaxConcurrency}, timeout={TimeoutSeconds}s, output={Output}",
            urls.Count,
            options.MaxConcurrency,
            options.RequestTimeout.TotalSeconds,
            options.OutputRoot);

        IReadOnlyList<DownloadResult> results;
        try
        {
            results = await orchestrator.RunAsync(urls, options, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Run canceled.");
            return 130;
        }
        finally
        {
            Log.CloseAndFlush();
        }

        var ok = results.Count(r => r.Success);
        var failed = results.Count - ok;

        logger.LogInformation("Completed. Success={SuccessCount} Failed={FailedCount}", ok, failed);

        if (failed > 0)
        {
            foreach (var r in results.Where(r => !r.Success))
            {
                logger.LogWarning("Failed: {Url} Error={Error}", r.Url, r.Error);
            }
        }

        return failed == 0 ? 0 : 1;
    }

    private static HostApplicationBuilder CreateHost(AppArguments parsed)
    {
        var builder = Host.CreateApplicationBuilder();

        var logDir = Path.GetFullPath("logs");
        Directory.CreateDirectory(logDir);

        var filePath = Path.Combine(logDir, "log-.log");

        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Information)
            .Enrich.FromLogContext()
            // Always write structured logs to file (human-readable text), one file per day.
            .WriteTo.File(
                filePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}");

        // Console output format is selectable.
        if (parsed.LogFormat == LogFormat.Text)
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Console(
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}");
        }
        else
        {
            loggerConfiguration = loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
        }

        Log.Logger = loggerConfiguration.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        builder.Services.AddSingleton<IContentWriter, FileSystemContentWriter>();

        // Typed client: created by IHttpClientFactory; no incorrect multiple HttpClient instances.
        builder.Services.AddHttpClient<IPageDownloader, HttpPageDownloader>(client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan; // per-request timeout via CancellationToken
                client.DefaultRequestVersion = HttpVersion.Version20;
                client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });

        builder.Services.AddSingleton<DownloadOrchestrator>();

        return builder;
    }
}
