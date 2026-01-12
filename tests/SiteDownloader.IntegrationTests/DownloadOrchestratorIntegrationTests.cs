using System.Net;
using SiteDownloader.Downloading;
using SiteDownloader.IO;

namespace SiteDownloader.IntegrationTests;

public sealed class DownloadOrchestratorIntegrationTests
{
    [Fact]
    public async Task Downloads_From_Local_Server_And_Writes_To_Disk()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var url = new Uri(server.BaseAddress, "/hello");

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
        Assert.True(results[0].Success);
        Assert.NotNull(results[0].OutputPath);
        Assert.True(File.Exists(results[0].OutputPath));

        var content = await File.ReadAllTextAsync(results[0].OutputPath!);
        Assert.Contains("hi", content);

        Directory.Delete(temp, recursive: true);
    }

    private static async Task<TestServerHandle> StartServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        // Kestrel doesn't support dynamic port binding via ListenLocalhost(0).
        // Bind explicitly to loopback with port 0 (dynamic).
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));

        var app = builder.Build();
        app.MapGet("/hello", () => Results.Text("hi", "text/plain"));

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
