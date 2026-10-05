using SmtOrderManager.Domain.Components;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Sqlite;

public sealed class SqliteDatabaseInitializerTests : IDisposable
{
    private readonly SqliteStorageTestContext _context = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task InitializeAsync_WithMissingDirectory_CreatesDatabaseWithSchema()
    {
        await CreateInitializer().InitializeAsync(Token);

        Assert.True(File.Exists(_context.DatabasePath));
        Assert.Empty(await new SqliteComponentRepository(_context.ContextFactory).SearchAsync(null, Token));
    }

    [Fact]
    public async Task InitializeAsync_WithExistingDatabase_KeepsData()
    {
        await CreateInitializer().InitializeAsync(Token);
        var repository = new SqliteComponentRepository(_context.ContextFactory);
        var component = Component.Create("RES-10K-0402", null);
        await repository.SaveAsync([component], Token);

        await CreateInitializer().InitializeAsync(Token);

        Assert.NotNull(await repository.GetByIdAsync(component.Id, Token));
    }

    private SqliteDatabaseInitializer CreateInitializer() =>
        new(_context.StorageOptions, _context.ContextFactory);
}
