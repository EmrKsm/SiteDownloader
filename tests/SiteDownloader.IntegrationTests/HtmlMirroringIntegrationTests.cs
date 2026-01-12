namespace SiteDownloader.IntegrationTests;

public sealed class HtmlMirroringIntegrationTests
{
    [Fact]
    public async Task Html_With_Css_And_Image_Is_Mirrored_Offline()
    {
        var temp = Path.Combine(Path.GetTempPath(), "SiteDownloaderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        await using var server = await StartServerAsync();
        var pageUrl = new Uri(server.BaseAddress, "/page");

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddLogging();

        builder.Services.AddSingleton<SiteDownloader.IContentWriter, SiteDownloader.FileSystemContentWriter>();
        builder.Services.AddHttpClient<SiteDownloader.IPageDownloader, SiteDownloader.HttpPageDownloader>()
            .ConfigureHttpClient(c => c.Timeout = Timeout.InfiniteTimeSpan);
        builder.Services.AddSingleton<SiteDownloader.Mirroring.IPageMirror, SiteDownloader.Mirroring.PageMirror>();
        builder.Services.AddSingleton<SiteDownloader.DownloadOrchestrator>();

        using var host = builder.Build();
        var orchestrator = host.Services.GetRequiredService<SiteDownloader.DownloadOrchestrator>();

        var options = new SiteDownloader.DownloadRunOptions(
            OutputRoot: temp,
            MaxConcurrency: 4,
            RequestTimeout: TimeSpan.FromSeconds(10),
            DownloadAssets: true,
            IncludeThirdPartyAssets: false);

        var results = await orchestrator.RunAsync(new[] { pageUrl }, options, CancellationToken.None);

        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.NotNull(results[0].OutputPath);

        var htmlPath = results[0].OutputPath!;
        Assert.True(File.Exists(htmlPath));

        var html = await File.ReadAllTextAsync(htmlPath);

        // HTML should have been rewritten to reference local files (not /style.css or /img.png)
        Assert.DoesNotContain("href=\"/style.css\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("src=\"/img.png\"", html, StringComparison.OrdinalIgnoreCase);

        // We should have saved at least a css and png somewhere under the same domain folder.
        var domainDir = Path.Combine(temp, "127.0.0.1");
        var files = Directory.EnumerateFiles(domainDir, "*", SearchOption.AllDirectories).ToArray();

        Assert.Contains(files, f => f.EndsWith(".css", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(files, f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

        Directory.Delete(temp, recursive: true);
    }

    private static async Task<TestServerHandle> StartServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(System.Net.IPAddress.Loopback, 0));

        var app = builder.Build();

        app.MapGet("/page", ctx =>
        {
            ctx.Response.ContentType = "text/html";
            return ctx.Response.WriteAsync(
                "<!doctype html><html><head><link rel=\"stylesheet\" href=\"/style.css\"></head>" +
                "<body><h1>hi</h1><img src=\"/img.png\"></body></html>");
        });

        app.MapGet("/style.css", ctx =>
        {
            ctx.Response.ContentType = "text/css";
            return ctx.Response.WriteAsync("body { background-image: url('/img.png'); }");
        });

        app.MapGet("/img.png", async ctx =>
        {
            ctx.Response.ContentType = "image/png";
            // 1x1 transparent PNG
            var png = Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMB/ax6qS8AAAAASUVORK5CYII=");
            await ctx.Response.Body.WriteAsync(png);
        });

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
