namespace SiteDownloader.UnitTests;

public sealed class UrlOutputPathTests
{
    [Fact]
    public void Root_Path_Goes_To_Domain_IndexHtml()
    {
        var file = SiteDownloader.UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/"), "text/html");

        Assert.EndsWith($"example.com{Path.DirectorySeparatorChar}index.html", file);
    }

    [Fact]
    public void Path_With_Segment_Goes_To_Domain_Subfolder_IndexHtml()
    {
        var file = SiteDownloader.UrlOutputPath.GetOutputFilePath("downloads", new Uri("https://example.com/a/b"), "text/html");

        Assert.EndsWith($"example.com{Path.DirectorySeparatorChar}a{Path.DirectorySeparatorChar}b{Path.DirectorySeparatorChar}index.html", file);
    }
}
