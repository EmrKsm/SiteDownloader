using SiteDownloader.App.Cli;

namespace SiteDownloader.UnitTests;

public sealed class AppArgumentsTests
{
    [Fact]
    public void Parse_Help_Flag_Returns_ShowHelp_True()
    {
        var result = AppArguments.TryParse(new[] { "--help" }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.True(parsed.ShowHelp);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    public void Parse_All_Help_Variants_Work(string helpFlag)
    {
        var result = AppArguments.TryParse(new[] { helpFlag }, out var parsed, out _);

        Assert.True(result);
        Assert.True(parsed.ShowHelp);
    }

    [Fact]
    public void Parse_Single_Url_Succeeds()
    {
        var result = AppArguments.TryParse(new[] { "--url", "https://example.com" }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Single(parsed.Urls);
        Assert.Equal("https://example.com", parsed.Urls[0]);
    }

    [Fact]
    public void Parse_Multiple_Url_Flags_Succeeds()
    {
        var result = AppArguments.TryParse(
            new[] { "--url", "https://example.com", "--url", "https://test.com" },
            out var parsed,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(2, parsed.Urls.Count);
        Assert.Equal("https://example.com", parsed.Urls[0]);
        Assert.Equal("https://test.com", parsed.Urls[1]);
    }

    [Fact]
    public void Parse_Urls_Comma_Separated_Succeeds()
    {
        var result = AppArguments.TryParse(
            new[] { "--urls", "https://example.com,https://test.com" },
            out var parsed,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(2, parsed.Urls.Count);
    }

    [Fact]
    public void Parse_File_Path_Succeeds()
    {
        var result = AppArguments.TryParse(new[] { "--file", "urls.txt" }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal("urls.txt", parsed.FilePath);
    }

    [Fact]
    public void Parse_MaxConcurrency_Succeeds()
    {
        var result = AppArguments.TryParse(new[] { "--max-concurrency", "20" }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(20, parsed.MaxConcurrency);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void Parse_Invalid_MaxConcurrency_Fails(string value)
    {
        var result = AppArguments.TryParse(new[] { "--max-concurrency", value }, out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("--max-concurrency", error);
    }

    [Fact]
    public void Parse_TimeoutSeconds_Succeeds()
    {
        var result = AppArguments.TryParse(new[] { "--timeout-seconds", "60" }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(60, parsed.TimeoutSeconds);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("invalid")]
    public void Parse_Invalid_TimeoutSeconds_Fails(string value)
    {
        var result = AppArguments.TryParse(new[] { "--timeout-seconds", value }, out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("--timeout-seconds", error);
    }

    [Fact]
    public void Parse_OutputPath_Succeeds()
    {
        var result = AppArguments.TryParse(new[] { "--output", "./myoutput" }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal("./myoutput", parsed.OutputRoot);
    }

    [Theory]
    [InlineData("json", LogFormat.Json)]
    [InlineData("JSON", LogFormat.Json)]
    [InlineData("text", LogFormat.Text)]
    [InlineData("TEXT", LogFormat.Text)]
    public void Parse_LogFormat_Succeeds(string value, LogFormat expected)
    {
        var result = AppArguments.TryParse(new[] { "--log-format", value }, out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(expected, parsed.LogFormat);
    }

    [Fact]
    public void Parse_Invalid_LogFormat_Fails()
    {
        var result = AppArguments.TryParse(new[] { "--log-format", "xml" }, out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("--log-format", error);
    }

    [Fact]
    public void Parse_Unknown_Argument_Fails()
    {
        var result = AppArguments.TryParse(new[] { "--unknown" }, out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("Unknown argument", error);
    }

    [Fact]
    public void Parse_Missing_Value_After_Flag_Fails()
    {
        var result = AppArguments.TryParse(new[] { "--url" }, out _, out var error);

        Assert.False(result);
        Assert.NotNull(error);
        Assert.Contains("Missing value", error);
    }

    [Fact]
    public void Parse_Empty_Args_Returns_Defaults()
    {
        var result = AppArguments.TryParse(Array.Empty<string>(), out var parsed, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(8, parsed.MaxConcurrency);
        Assert.Equal(30, parsed.TimeoutSeconds);
        Assert.Equal("downloads", parsed.OutputRoot);
        Assert.Equal(LogFormat.Text, parsed.LogFormat);
        Assert.False(parsed.ShowHelp);
    }

    [Fact]
    public void Parse_Combined_Options_Succeeds()
    {
        var result = AppArguments.TryParse(
            new[] {
                "--url", "https://example.com",
                "--max-concurrency", "16",
                "--timeout-seconds", "45",
                "--output", "./out",
                "--log-format", "json"
            },
            out var parsed,
            out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Single(parsed.Urls);
        Assert.Equal(16, parsed.MaxConcurrency);
        Assert.Equal(45, parsed.TimeoutSeconds);
        Assert.Equal("./out", parsed.OutputRoot);
        Assert.Equal(LogFormat.Json, parsed.LogFormat);
    }

    [Fact]
    public void HelpText_Contains_Usage_Information()
    {
        var help = AppArguments.HelpText;

        Assert.Contains("Usage:", help);
        Assert.Contains("--url", help);
        Assert.Contains("--max-concurrency", help);
        Assert.Contains("--log-format", help);
    }
}
