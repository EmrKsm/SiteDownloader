using System.Net;
using SiteDownloader.Downloading;
using SiteDownloader.IO;

namespace SiteDownloader.IntegrationTests;

public sealed class DownloadOrchestratorErrorHandlingTests
{
    [Fact]
    public async Task Download_With_404_Returns_Failed_Result()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var url = new Uri(server.BaseAddress, "/notfound");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IContentWriter, FileSystemContentWriter>();
        builder.Services.AddHttpClient<IPageDownloader, HttpPageDownloader>()
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<DownloadOrchestrator>();

        using var host = builder.Build();
        var orchestrator = host.Services.GetRequiredService<DownloadOrchestrator>();

        var options = new DownloadRunOptions(
            OutputRoot: temp,
            MaxConcurrency: 2,
            RequestTimeout: TimeSpan.FromSeconds(10));

        var results = await orchestrator.RunAsync(new[] { url }, options, CancellationToken.None);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Equal(HttpStatusCode.NotFound, results[0].StatusCode);
        Assert.Null(results[0].OutputPath);
        Assert.NotNull(results[0].Error);

        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task Download_Multiple_Urls_With_Mixed_Results()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var urls = new[]
        {
            new Uri(server.BaseAddress, "/success"),
            new Uri(server.BaseAddress, "/notfound"),
            new Uri(server.BaseAddress, "/success2")
        };

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IContentWriter, FileSystemContentWriter>();
        builder.Services.AddHttpClient<IPageDownloader, HttpPageDownloader>()
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<DownloadOrchestrator>();

        using var host = builder.Build();
        var orchestrator = host.Services.GetRequiredService<DownloadOrchestrator>();

        var options = new DownloadRunOptions(
            OutputRoot: temp,
            MaxConcurrency: 2,
            RequestTimeout: TimeSpan.FromSeconds(10));

        var results = await orchestrator.RunAsync(urls, options, CancellationToken.None);

        Assert.Equal(3, results.Count);
        Assert.Equal(2, results.Count(r => r.Success));
        Assert.Single(results.Where(r => !r.Success));

        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task Download_With_Cancellation_Returns_Canceled_Results()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var url = new Uri(server.BaseAddress, "/slow");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IContentWriter, FileSystemContentWriter>();
        builder.Services.AddHttpClient<IPageDownloader, HttpPageDownloader>()
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<DownloadOrchestrator>();

        using var host = builder.Build();
        var orchestrator = host.Services.GetRequiredService<DownloadOrchestrator>();

        var options = new DownloadRunOptions(
            OutputRoot: temp,
            MaxConcurrency: 2,
            RequestTimeout: TimeSpan.FromSeconds(10));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await orchestrator.RunAsync(new[] { url }, options, cts.Token));

        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task Download_With_Zero_Concurrency_Throws_ArgumentOutOfRangeException()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var url = new Uri(server.BaseAddress, "/success");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IContentWriter, FileSystemContentWriter>();
        builder.Services.AddHttpClient<IPageDownloader, HttpPageDownloader>()
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<DownloadOrchestrator>();

        using var host = builder.Build();
        var orchestrator = host.Services.GetRequiredService<DownloadOrchestrator>();

        var options = new DownloadRunOptions(
            OutputRoot: temp,
            MaxConcurrency: 0,
            RequestTimeout: TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await orchestrator.RunAsync(new[] { url }, options, CancellationToken.None));

        Directory.Delete(temp, recursive: true);
    }

    [Fact]
    public async Task Download_With_High_Concurrency_Works()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var urls = Enumerable.Range(1, 20)
            .Select(i => new Uri(server.BaseAddress, $"/page{i}"))
            .ToArray();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<IContentWriter, FileSystemContentWriter>();
        builder.Services.AddHttpClient<IPageDownloader, HttpPageDownloader>()
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<DownloadOrchestrator>();

        using var host = builder.Build();
        var orchestrator = host.Services.GetRequiredService<DownloadOrchestrator>();

        var options = new DownloadRunOptions(
            OutputRoot: temp,
            MaxConcurrency: 10,
            RequestTimeout: TimeSpan.FromSeconds(10));

        var results = await orchestrator.RunAsync(urls, options, CancellationToken.None);

        Assert.Equal(20, results.Count);
        Assert.All(results, r => Assert.True(r.Success));

        Directory.Delete(temp, recursive: true);
    }

    private static async Task<TestServerHandle> StartServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));

        var app = builder.Build();

        app.MapGet("/success", () => Results.Text("success", "text/plain"));
        app.MapGet("/success2", () => Results.Text("success2", "text/plain"));
        app.MapGet("/notfound", () => Results.NotFound());
        app.MapGet("/slow", async () =>
        {
            await Task.Delay(5000);
            return Results.Text("slow", "text/plain");
        });

        // Dynamic pages for concurrency test
        app.MapGet("/page{id:int}", (int id) => Results.Text($"Page {id}", "text/plain"));

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(address))
        {
            await app.DisposeAsync();
            throw new InvalidOperationException("Failed to determine server address.");
        }

        return new TestServerHandle(app, new Uri(address));
    }

    private sealed record TestServerHandle(WebApplication App, Uri BaseAddress) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }
}
