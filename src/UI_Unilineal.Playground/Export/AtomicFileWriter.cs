namespace UI_Unilineal.Playground.Export;

public sealed class AtomicFileWriter
{
    public async ValueTask WriteAsync(
        string destinationPath,
        Func<Stream, CancellationToken, ValueTask> writeAsync,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException(
                "Destination path is required.",
                nameof(destinationPath));
        }

        ArgumentNullException.ThrowIfNull(writeAsync);
        cancellationToken.ThrowIfCancellationRequested();

        string fullPath =
            Path.GetFullPath(destinationPath);
        string? directory =
            Path.GetDirectoryName(fullPath);

        if (string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Destination directory does not exist: '{directory}'.");
        }

        string fileName =
            Path.GetFileName(fullPath);
        string temporaryPath =
            Path.Combine(
                directory,
                $".{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous |
                FileOptions.WriteThrough))
            {
                await writeAsync(
                    stream,
                    cancellationToken);
                await stream.FlushAsync(
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(fullPath))
            {
                File.Replace(
                    temporaryPath,
                    fullPath,
                    destinationBackupFileName: null,
                    ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(
                    temporaryPath,
                    fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
