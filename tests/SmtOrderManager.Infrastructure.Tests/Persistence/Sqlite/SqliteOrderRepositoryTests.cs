using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;

namespace SmtOrderManager.Infrastructure.Tests.Persistence.Sqlite;

/// <summary>
/// Runs the order repository contract against a SQLite database file and covers the stored
/// status.
/// </summary>
public sealed class SqliteOrderRepositoryTests : OrderRepositoryContractTests, IDisposable
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));

    private readonly SqliteStorageTestContext _context = new();

    public SqliteOrderRepositoryTests() => _context.CreateDatabase();

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SaveAsync_StoresStatusAsFixedText()
    {
        var order = Order.Create("Week 41", null, IssuedAt, [new OrderLine(Guid.NewGuid(), 500)]);
        order.MarkDownloaded(IssuedAt.AddDays(1));

        await CreateRepository().SaveAsync([order], Token);

        await using var db = await _context.ContextFactory.CreateDbContextAsync(Token);
        var status = await db.Orders.Where(row => row.Id == order.Id).Select(row => row.Status).SingleAsync(Token);
        Assert.Equal(SqliteOrderRepository.DownloadedStatus, status);
    }

    [Fact]
    public async Task GetByIdAsync_WithUnknownStoredStatus_ThrowsInvalidDataExceptionNamingOrder()
    {
        var id = Guid.NewGuid();
        await using (var db = await _context.ContextFactory.CreateDbContextAsync(Token))
        {
            db.Orders.Add(new OrderRow
            {
                Id = id,
                Name = "Week 41",
                OrderDate = IssuedAt,
                Lines = [new OrderLineRow { OrderId = id, BoardId = Guid.NewGuid(), Position = 0, Quantity = 500 }],
                Status = "Shipped",
            });
            await db.SaveChangesAsync(Token);
        }

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => CreateRepository().GetByIdAsync(id, Token));

        Assert.Contains($"order {id}", exception.Message);
    }

    protected override IOrderRepository CreateRepository() =>
        new SqliteOrderRepository(_context.ContextFactory);
}
