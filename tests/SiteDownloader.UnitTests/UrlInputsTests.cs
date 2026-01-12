using SiteDownloader.App.Cli;

namespace SiteDownloader.UnitTests;

public sealed class UrlInputsTests
{
    [Fact]
    public async Task GetUrlsAsync_Returns_Urls_From_Arguments()
    {
        var args = new AppArguments(
            Urls: new[] { "https://example.com", "https://test.com" },
            FilePath: null,
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

        Assert.Equal(2, urls.Count);
        Assert.Equal("https://example.com/", urls[0].ToString());
        Assert.Equal("https://test.com/", urls[1].ToString());
    }

    [Fact]
    public async Task GetUrlsAsync_Removes_Duplicates()
    {
        var args = new AppArguments(
            Urls: new[] { "https://example.com", "https://example.com" },
            FilePath: null,
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

        Assert.Single(urls);
    }

    [Fact]
    public async Task GetUrlsAsync_Adds_Https_Scheme_If_Missing()
    {
        var args = new AppArguments(
            Urls: new[] { "example.com" },
            FilePath: null,
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

        Assert.Single(urls);
        Assert.Equal("https", urls[0].Scheme);
    }

    [Fact]
    public async Task GetUrlsAsync_Filters_Invalid_Urls()
    {
        var args = new AppArguments(
            Urls: new[] { "https://example.com", "ht!tp://not valid", "https://test.com" },
            FilePath: null,
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

        Assert.Equal(2, urls.Count);
    }

    [Fact]
    public async Task GetUrlsAsync_Filters_Empty_Strings()
    {
        var args = new AppArguments(
            Urls: new[] { "https://example.com", "", "  ", "https://test.com" },
            FilePath: null,
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

        Assert.Equal(2, urls.Count);
    }

    [Fact]
    public async Task GetUrlsAsync_From_File_Reads_Lines()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile, new[]
            {
                "https://example.com",
                "https://test.com"
            });

            var args = new AppArguments(
                Urls: Array.Empty<string>(),
                FilePath: tempFile,
                MaxConcurrency: 8,
                TimeoutSeconds: 30,
                OutputRoot: "downloads",
                LogFormat: LogFormat.Text,
                ShowHelp: false);

            var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

            Assert.Equal(2, urls.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetUrlsAsync_From_File_Ignores_Comments()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile, new[]
            {
                "# This is a comment",
                "https://example.com",
                "# Another comment",
                "https://test.com"
            });

            var args = new AppArguments(
                Urls: Array.Empty<string>(),
                FilePath: tempFile,
                MaxConcurrency: 8,
                TimeoutSeconds: 30,
                OutputRoot: "downloads",
                LogFormat: LogFormat.Text,
                ShowHelp: false);

            var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

            Assert.Equal(2, urls.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetUrlsAsync_From_File_Ignores_Empty_Lines()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile, new[]
            {
                "https://example.com",
                "",
                "   ",
                "https://test.com"
            });

            var args = new AppArguments(
                Urls: Array.Empty<string>(),
                FilePath: tempFile,
                MaxConcurrency: 8,
                TimeoutSeconds: 30,
                OutputRoot: "downloads",
                LogFormat: LogFormat.Text,
                ShowHelp: false);

            var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

            Assert.Equal(2, urls.Count);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetUrlsAsync_Missing_File_Throws_FileNotFoundException()
    {
        var args = new AppArguments(
            Urls: Array.Empty<string>(),
            FilePath: "nonexistent-file.txt",
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await UrlInputs.GetUrlsAsync(args, CancellationToken.None));
    }

    [Fact]
    public async Task GetUrlsAsync_Trims_Whitespace()
    {
        var args = new AppArguments(
            Urls: new[] { "  https://example.com  " },
            FilePath: null,
            MaxConcurrency: 8,
            TimeoutSeconds: 30,
            OutputRoot: "downloads",
            LogFormat: LogFormat.Text,
            ShowHelp: false);

        var urls = await UrlInputs.GetUrlsAsync(args, CancellationToken.None);

        Assert.Single(urls);
        Assert.Equal("https://example.com/", urls[0].ToString());
    }
}
