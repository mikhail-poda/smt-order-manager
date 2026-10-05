using System.Text.Json;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// Stores the documents of one aggregate type as a JSON array in a single file.
/// </summary>
/// <remarks>
/// <para>
/// Writes are atomic: the new content is written to a temporary file next to the data file,
/// flushed to disk and then moved over the data file. A crash or a failed write therefore
/// leaves either the old or the new content, never a half-written file.
/// </para>
/// <para>
/// Every read and every read-modify-write runs under one lock, so concurrent updates cannot
/// overwrite each other. The lock belongs to the instance, so there must be exactly one
/// instance per file, registered as a singleton. Access from other processes is not
/// coordinated, which is acceptable for a single-user console application.
/// </para>
/// <para>
/// The store owns its lock and is therefore disposable. As a singleton it is disposed by the
/// dependency injection container when the application shuts down.
/// </para>
/// </remarks>
/// <typeparam name="TDocument">The persistence document type of the aggregate.</typeparam>
internal sealed class JsonFileStore<TDocument> : IDisposable
    where TDocument : class
{
    private const string TemporaryFileExtension = ".tmp";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _temporaryPath;

    public JsonFileStore(JsonStorageOptions options, string fileName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DataDirectory, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("The file name must not contain a directory.", nameof(fileName));
        }

        FilePath = Path.Combine(Path.GetFullPath(options.DataDirectory), fileName);
        _temporaryPath = FilePath + TemporaryFileExtension;
    }

    /// <summary>
    /// The full path of the data file.
    /// </summary>
    public string FilePath { get; }

    public void Dispose() => _lock.Dispose();

    /// <summary>
    /// Loads all documents. A missing data file means no documents have been stored yet.
    /// </summary>
    /// <exception cref="InvalidDataException">The data file does not contain a valid document list.</exception>
    public async Task<IReadOnlyList<TDocument>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            return await ReadAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Loads all documents, applies the change and writes the result in one atomic step.
    /// </summary>
    /// <param name="change">Returns the new document list from the current one.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <remarks>
    /// When <paramref name="change"/> throws, nothing is written and the exception is passed on.
    /// </remarks>
    /// <exception cref="InvalidDataException">The data file does not contain a valid document list.</exception>
    public async Task UpdateAsync(
        Func<IReadOnlyList<TDocument>, IReadOnlyList<TDocument>> change,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        await _lock.WaitAsync(cancellationToken);

        try
        {
            var current = await ReadAsync(cancellationToken);
            var updated = change(current)
                ?? throw new InvalidOperationException("The change must return a document list.");

            await WriteAsync(updated, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IReadOnlyList<TDocument>> ReadAsync(CancellationToken cancellationToken)
    {
        FileStream stream;

        try
        {
            stream = new FileStream(FilePath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous,
            });
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }

        await using (stream)
        {
            List<TDocument>? documents;

            try
            {
                documents = await JsonSerializer.DeserializeAsync<List<TDocument>>(
                    stream,
                    JsonStorageSerializer.Options,
                    cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"The data file '{FilePath}' does not contain valid JSON.", exception);
            }

            // The annotations do not stop a file from containing null, so it is checked here.
            if (documents is null || documents.Exists(document => document is null))
            {
                throw new InvalidDataException($"The data file '{FilePath}' does not contain a list of documents.");
            }

            return documents;
        }
    }

    private async Task WriteAsync(IReadOnlyList<TDocument> documents, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        try
        {
            await using (var stream = new FileStream(_temporaryPath, new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            }))
            {
                await JsonSerializer.SerializeAsync(stream, documents, JsonStorageSerializer.Options, cancellationToken);

                // Without flushing to disk, the move could reach the disk before the content.
                stream.Flush(flushToDisk: true);
            }

            File.Move(_temporaryPath, FilePath, overwrite: true);
        }
        catch
        {
            TryDeleteTemporaryFile();
            throw;
        }
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            File.Delete(_temporaryPath);
        }
        catch (IOException)
        {
            // The original exception matters more. A leftover temporary file is overwritten
            // by the next write and never read.
        }
        catch (UnauthorizedAccessException)
        {
            // See above.
        }
    }
}