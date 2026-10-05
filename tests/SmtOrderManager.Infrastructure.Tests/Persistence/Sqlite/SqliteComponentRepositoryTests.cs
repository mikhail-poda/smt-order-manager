using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Sqlite;

/// <summary>
/// Runs the repository contract against a SQLite database file and covers damaged data,
/// through the component repository.
/// </summary>
public sealed class SqliteComponentRepositoryTests : ComponentRepositoryContractTests, IDisposable
{
    private readonly SqliteStorageTestContext _context = new();

    public SqliteComponentRepositoryTests() => _context.CreateDatabase();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GetByIdAsync_WithStoredDataBreakingDomainRule_ThrowsInvalidDataExceptionNamingAggregate()
    {
        var id = Guid.NewGuid();
        await using (var db = await _context.ContextFactory.CreateDbContextAsync(Token))
        {
            db.Components.Add(new ComponentRow { Id = id, Name = "   ", Description = string.Empty });
            await db.SaveChangesAsync(Token);
        }

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateRepository().GetByIdAsync(id, Token));

        Assert.Contains($"component {id}", exception.Message);
    }

    protected override IComponentRepository CreateRepository() =>
        new SqliteComponentRepository(_context.ContextFactory);
}
