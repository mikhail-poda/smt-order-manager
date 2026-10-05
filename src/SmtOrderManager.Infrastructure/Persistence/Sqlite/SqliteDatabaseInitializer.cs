using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Prepares the database file before the repositories use it: creates the directory and, if
/// the file is new, the schema. Runs as a hosted service, so every host prepares the database
/// when it starts.
/// </summary>
/// <remarks>
/// <c>EnsureCreated</c> creates the schema of a new database and leaves an existing one as it
/// is. It cannot evolve an existing schema; that would need migrations.
/// </remarks>
internal sealed class SqliteDatabaseInitializer(
    SqliteStorageOptions options,
    IDbContextFactory<SmtOrderManagerDbContext> contextFactory) : IHostedService
{
    private readonly SqliteStorageOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    private readonly IDbContextFactory<SmtOrderManagerDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public Task StartAsync(CancellationToken cancellationToken) => InitializeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(_options.DatabasePath));

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}