using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Sqlite;

/// <summary>
/// A database file in a temporary directory for repository tests. Deletes the directory when
/// disposed.
/// </summary>
/// <remarks>
/// The tests use a real file rather than an in-memory database, so they exercise the same file
/// handling as the application. Connection pooling is off, so no connection keeps the file open
/// and the directory can be deleted, also on Windows.
/// </remarks>
internal sealed class SqliteStorageTestContext : IDisposable
{
    private readonly DbContextOptions<SmtOrderManagerDbContext> _options;

    public SqliteStorageTestContext()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Pooling = false,
        }.ToString();

        _options = new DbContextOptionsBuilder<SmtOrderManagerDbContext>()
            .UseSqlite(connectionString)
            .Options;

        ContextFactory = new TestDbContextFactory(_options);
    }

    public string DataDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "smt-order-manager-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    /// The database file, in a subdirectory that does not exist until the database is created.
    /// </summary>
    public string DatabasePath => Path.Combine(DataDirectory, "db", "smt-order-manager.db");

    public IDbContextFactory<SmtOrderManagerDbContext> ContextFactory { get; }

    public SqliteStorageOptions StorageOptions => new() { DatabasePath = DatabasePath };

    /// <summary>
    /// Creates the directory and the schema, for tests that start with an empty database.
    /// </summary>
    public void CreateDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);

        using var db = ContextFactory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        if (Directory.Exists(DataDirectory))
        {
            Directory.Delete(DataDirectory, recursive: true);
        }
    }

    private sealed class TestDbContextFactory(DbContextOptions<SmtOrderManagerDbContext> options)
        : IDbContextFactory<SmtOrderManagerDbContext>
    {
        public SmtOrderManagerDbContext CreateDbContext() => new(options);
    }
}
