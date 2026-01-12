namespace SiteDownloader;

public sealed class FileSystemContentWriter : IContentWriter
{
    public async Task<string> SaveAsync(Uri url, HttpResponseMessage response, string outputRoot, CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.ToString();
        var outputFile = UrlOutputPath.GetOutputFilePath(outputRoot, url, contentType);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            outputFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        return outputFile;
    }
}
