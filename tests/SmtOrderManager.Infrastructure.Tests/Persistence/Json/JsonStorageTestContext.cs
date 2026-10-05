using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

/// <summary>
/// A temporary data directory for repository tests. Creates stores on it and deletes it, with
/// every store it created, when disposed.
/// </summary>
/// <remarks>
/// Each call to <see cref="CreateStore{TDocument}"/> returns a new store instance. Saving through
/// one instance and loading through another therefore reads the file from disk, as after an
/// application restart.
/// </remarks>
internal sealed class JsonStorageTestContext : IDisposable
{
    private readonly List<IDisposable> _stores = [];

    public string DataDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "smt-order-manager-tests", Guid.NewGuid().ToString("N"));

    public JsonFileStore<TDocument> CreateStore<TDocument>(string fileName)
        where TDocument : class
    {
        var store = new JsonFileStore<TDocument>(new JsonStorageOptions { DataDirectory = DataDirectory }, fileName);
        _stores.Add(store);

        return store;
    }

    /// <summary>
    /// Writes a data file directly, for arranging stored data that the repositories would not write.
    /// </summary>
    public async Task WriteFileAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(Path.Combine(DataDirectory, fileName), content, cancellationToken);
    }

    public void Dispose()
    {
        foreach (var store in _stores)
        {
            store.Dispose();
        }

        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }
}
