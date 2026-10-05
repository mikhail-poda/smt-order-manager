using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Boards;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Sqlite;

/// <summary>
/// Runs the board repository contract against a SQLite database file and checks that no BOM
/// rows are left behind.
/// </summary>
public sealed class SqliteBoardRepositoryTests : BoardRepositoryContractTests, IDisposable
{
    private readonly SqliteStorageTestContext _context = new();

    public SqliteBoardRepositoryTests() => _context.CreateDatabase();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SaveAsync_WithShorterBillOfMaterials_DeletesRemovedEntryRows()
    {
        var repository = CreateRepository();
        var resistorId = Guid.NewGuid();
        var board = Board.Create(
            "Controller",
            null,
            100m,
            80m,
            [new BomEntry(resistorId, 12), new BomEntry(Guid.NewGuid(), 3)]);
        await repository.SaveAsync([board], Token);

        board.RemoveBomEntry(board.BillOfMaterials[1].ComponentId);
        await repository.SaveAsync([board], Token);

        Assert.Equal(new[] { resistorId }, await StoredBomComponentIdsAsync());
    }

    [Fact]
    public async Task RemoveAsync_DeletesBillOfMaterialsRows()
    {
        var repository = CreateRepository();
        var board = Board.Create("Controller", null, 100m, 80m, [new BomEntry(Guid.NewGuid(), 12)]);
        await repository.SaveAsync([board], Token);

        await repository.RemoveAsync([board.Id], Token);

        Assert.Empty(await StoredBomComponentIdsAsync());
    }

    protected override IBoardRepository CreateRepository() =>
        new SqliteBoardRepository(_context.ContextFactory);

    private async Task<List<Guid>> StoredBomComponentIdsAsync()
    {
        await using var db = await _context.ContextFactory.CreateDbContextAsync(Token);

        return await db.BomEntries.Select(entry => entry.ComponentId).ToListAsync(Token);
    }
}
