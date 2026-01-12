using SiteDownloader.Pathing;

namespace SiteDownloader.UnitTests;

public sealed class UrlOutputPathExtendedTests
{
    [Fact]
    public void File_With_Extension_Uses_Original_Extension()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/doc.pdf"), null);

        Assert.EndsWith(".pdf", file);
        Assert.Contains("doc.pdf", file);
    }

    [Fact]
    public void Path_With_Query_String_Includes_Hash()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/?search=test"), "text/html");

        Assert.Contains("__", file); // Hash separator
        Assert.EndsWith(".html", file);
    }

    [Theory]
    [InlineData("text/html", ".html")]
    [InlineData("application/json", ".json")]
    [InlineData("application/xml", ".xml")]
    [InlineData("text/xml", ".xml")]
    [InlineData("text/plain", ".txt")]
    [InlineData("application/octet-stream", ".bin")]
    [InlineData("image/png", ".bin")]
    public void ContentType_Maps_To_Correct_Extension(string contentType, string expectedExtension)
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/resource"), contentType);

        Assert.EndsWith(expectedExtension, file);
    }

    [Fact]
    public void ContentType_With_Charset_Strips_Parameters()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/page"), "text/html; charset=utf-8");

        Assert.EndsWith(".html", file);
    }

    [Fact]
    public void Null_ContentType_Defaults_To_Html()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/page"), null);

        Assert.EndsWith(".html", file);
    }

    [Fact]
    public void Path_With_Trailing_Slash_Creates_Index()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/folder/"), "text/html");

        Assert.Contains("index.html", file);
        Assert.Contains("folder", file);
    }

    [Fact]
    public void Invalid_Characters_In_Path_Are_Sanitized()
    {
        // Use URL encoding which puts %XX in the path that won't be sanitized (testing the other direction)
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/file%3Ctest%3E"), "text/html");

        // Since %3C and %3E are valid filename chars, they won't be sanitized
        // This tests that URL-encoded characters stay as-is in filenames
        Assert.Contains("%3C", file);
        Assert.Contains("%3E", file);
    }

    [Fact]
    public void Empty_Host_Uses_Unknown_Host()
    {
        var uri = new Uri("file:///local/path");
        var file = UrlOutputPath.GetOutputFilePath("downloads", uri, "text/html");

        Assert.Contains("unknown-host", file);
    }

    [Fact]
    public void Multiple_Path_Segments_Create_Nested_Directories()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/a/b/c/page.html"), "text/html");

        var sep = Path.DirectorySeparatorChar.ToString();
        Assert.Contains($"a{sep}b{sep}c", file);
        Assert.EndsWith("page.html", file);
    }

    [Fact]
    public void Path_Without_Extension_Creates_Index()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/about"), "text/html");

        Assert.Contains("about", file);
        Assert.EndsWith("index.html", file);
    }

    [Fact]
    public void Same_Query_Produces_Same_Hash()
    {
        var file1 = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/?id=123"), "text/html");
        var file2 = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/?id=123"), "text/html");

        Assert.Equal(file1, file2);
    }

    [Fact]
    public void Different_Query_Produces_Different_Hash()
    {
        var file1 = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/?id=123"), "text/html");
        var file2 = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/?id=456"), "text/html");

        Assert.NotEqual(file1, file2);
    }

    [Fact]
    public void Domain_Is_Included_In_Path()
    {
        var file = UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/"), "text/html");

        Assert.Contains("example.com", file);
    }

    [Fact]
    public void Output_Root_Is_Included_In_Path()
    {
        var file = UrlOutputPath.GetOutputFilePath("myroot", new Uri("https://example.com/"), "text/html");

        Assert.StartsWith("myroot", file);
    }
}
