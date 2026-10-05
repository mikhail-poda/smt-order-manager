using SmtOrderManager.Infrastructure.Persistence.Json;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Json;

public sealed class JsonFileStoreTests : IDisposable
{
    private const string FileName = "documents.json";

    private readonly string _rootDirectory =
        Path.Combine(Path.GetTempPath(), "smt-order-manager-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A subdirectory that does not exist yet, so the tests also cover its creation.
    private string DataDirectory => Path.Combine(_rootDirectory, "data");

    private string DataFilePath => Path.Combine(DataDirectory, FileName);

    private string TemporaryFilePath => DataFilePath + ".tmp";

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithoutDataFile_ReturnsEmptyListAndCreatesNothing()
    {
        using var store = CreateStore();

        var documents = await store.LoadAsync(Token);

        Assert.Empty(documents);
        Assert.False(Directory.Exists(DataDirectory));
    }

    [Fact]
    public async Task UpdateAsync_ThenLoadAsync_ReturnsStoredDocuments()
    {
        using var store = CreateStore();
        var resistor = new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft);
        var capacitor = new TestDocument(Guid.NewGuid(), "CAP-100N-0402", TestStatus.Downloaded);

        await store.UpdateAsync(_ => [resistor, capacitor], Token);

        Assert.Equal([resistor, capacitor], await store.LoadAsync(Token));
    }

    [Fact]
    public async Task LoadAsync_WithNewInstanceOnSameFile_ReturnsStoredDocuments()
    {
        var document = new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft);
        using (var writer = CreateStore())
        {
            await writer.UpdateAsync(_ => [document], Token);
        }

        using var reader = CreateStore();
        var reloaded = await reader.LoadAsync(Token);

        Assert.Equal([document], reloaded);
    }

    [Fact]
    public async Task UpdateAsync_PassesCurrentDocumentsToChange()
    {
        using var store = CreateStore();
        var first = new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft);
        var second = new TestDocument(Guid.NewGuid(), "CAP-100N-0402", TestStatus.Draft);
        await store.UpdateAsync(_ => [first], Token);

        await store.UpdateAsync(current => [.. current, second], Token);

        Assert.Equal([first, second], await store.LoadAsync(Token));
    }

    [Fact]
    public async Task UpdateAsync_WritesReadableJsonWithEnumsAsNames()
    {
        using var store = CreateStore();

        await store.UpdateAsync(_ => [new TestDocument(Guid.NewGuid(), "Prüfauftrag", TestStatus.Downloaded)], Token);

        var json = await File.ReadAllTextAsync(DataFilePath, Token);
        Assert.Contains("\"name\": \"Prüfauftrag\"", json);
        Assert.Contains("\"status\": \"downloaded\"", json);
    }

    [Fact]
    public async Task UpdateAsync_AfterSuccessfulWrite_LeavesNoTemporaryFile()
    {
        using var store = CreateStore();

        await store.UpdateAsync(_ => [new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft)], Token);

        Assert.True(File.Exists(DataFilePath));
        Assert.False(File.Exists(TemporaryFilePath));
    }

    [Fact]
    public async Task UpdateAsync_WhenChangeThrows_KeepsDataFileUnchanged()
    {
        using var store = CreateStore();
        await store.UpdateAsync(_ => [new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft)], Token);
        var before = await File.ReadAllTextAsync(DataFilePath, Token);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpdateAsync(_ => throw new InvalidOperationException("Simulated failure."), Token));

        Assert.Equal(before, await File.ReadAllTextAsync(DataFilePath, Token));
        Assert.False(File.Exists(TemporaryFilePath));
    }

    [Fact]
    public async Task UpdateAsync_AfterChangeThrew_StillAcceptsUpdates()
    {
        using var store = CreateStore();
        var document = new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.UpdateAsync(_ => throw new InvalidOperationException("Simulated failure."), Token));
        await store.UpdateAsync(_ => [document], Token);

        Assert.Equal([document], await store.LoadAsync(Token));
    }

    [Fact]
    public async Task LoadAsync_WithLeftoverTemporaryFileFromCrash_IgnoresIt()
    {
        using var store = CreateStore();
        var document = new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft);
        await store.UpdateAsync(_ => [document], Token);
        await File.WriteAllTextAsync(TemporaryFilePath, "[{\"id\": \"half-writ", Token);

        Assert.Equal([document], await store.LoadAsync(Token));
    }

    [Fact]
    public async Task UpdateAsync_WithLeftoverTemporaryFileFromCrash_ReplacesIt()
    {
        using var store = CreateStore();
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(TemporaryFilePath, "[{\"id\": \"half-writ", Token);
        var document = new TestDocument(Guid.NewGuid(), "RES-10K-0402", TestStatus.Draft);

        await store.UpdateAsync(_ => [document], Token);

        Assert.Equal([document], await store.LoadAsync(Token));
        Assert.False(File.Exists(TemporaryFilePath));
    }

    [Fact]
    public async Task UpdateAsync_WithConcurrentCalls_LosesNoUpdate()
    {
        using var store = CreateStore();
        var documents = Enumerable
            .Range(0, 50)
            .Select(index => new TestDocument(Guid.NewGuid(), $"Component {index}", TestStatus.Draft))
            .ToList();

        await AppendConcurrentlyAsync(store, documents);

        var stored = await store.LoadAsync(Token);
        Assert.Equal(
            documents.Select(document => document.Id).Order(),
            stored.Select(document => document.Id).Order());
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{\"id\": \"5f0c\"}")]
    [InlineData("null")]
    [InlineData("[null]")]
    public async Task LoadAsync_WithInvalidContent_ThrowsInvalidDataExceptionNamingFile(string content)
    {
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(DataFilePath, content, Token);
        using var store = CreateStore();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(Token));

        Assert.Contains(DataFilePath, exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidContent_DoesNotOverwriteDataFile()
    {
        Directory.CreateDirectory(DataDirectory);
        await File.WriteAllTextAsync(DataFilePath, "{ not json", Token);
        using var store = CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.UpdateAsync(_ => [], Token));

        Assert.Equal("{ not json", await File.ReadAllTextAsync(DataFilePath, Token));
    }

    [Fact]
    public async Task LoadAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.LoadAsync(cancellation.Token));
    }

    [Fact]
    public void Constructor_WithRelativeDataDirectory_ResolvesFullPath()
    {
        using var store = new JsonFileStore<TestDocument>(new JsonStorageOptions { DataDirectory = "data" }, FileName);

        Assert.True(Path.IsPathFullyQualified(store.FilePath));
        Assert.EndsWith(Path.Combine("data", FileName), store.FilePath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankDataDirectory_Throws(string dataDirectory)
    {
        Assert.Throws<ArgumentException>(() => CreateStore(dataDirectory, FileName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("nested/documents.json")]
    public void Constructor_WithInvalidFileName_Throws(string fileName)
    {
        Assert.Throws<ArgumentException>(() => CreateStore(DataDirectory, fileName));
    }

    /// <summary>
    /// Appends each document in its own update, all started at once on the thread pool.
    /// </summary>
    private static Task AppendConcurrentlyAsync(JsonFileStore<TestDocument> store, IEnumerable<TestDocument> documents) =>
        Task.WhenAll(documents.Select(document =>
            Task.Run(() => store.UpdateAsync(current => [.. current, document], Token), Token)));

    private JsonFileStore<TestDocument> CreateStore() => CreateStore(DataDirectory, FileName);

    private static JsonFileStore<TestDocument> CreateStore(string dataDirectory, string fileName) =>
        new(new JsonStorageOptions { DataDirectory = dataDirectory }, fileName);

    internal enum TestStatus
    {
        Draft,
        Downloaded,
    }

    internal sealed record TestDocument(Guid Id, string Name, TestStatus Status);
}