using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Stores orders in the <c>Orders</c> table and their lines in <c>OrderLines</c>.
/// </summary>
/// <remarks>
/// The status is stored as text with its own fixed values, so renaming a domain enum value
/// cannot silently change the meaning of stored data.
/// </remarks>
internal sealed class SqliteOrderRepository(IDbContextFactory<SmtOrderManagerDbContext> contextFactory)
    : SqliteRepository<Order, OrderRow>(contextFactory), IOrderRepository
{
    public const string DraftStatus = "Draft";
    public const string DownloadedStatus = "Downloaded";

    public Task<IReadOnlyList<Order>> FindOrdersUsingBoardsAsync(
        IReadOnlyCollection<Guid> boardIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(boardIds);

        var wanted = boardIds.ToList();

        return FindAsync(
            rows => rows.Where(row => row.Lines.Any(line => wanted.Contains(line.BoardId))),
            cancellationToken);
    }

    protected override IQueryable<OrderRow> IncludeChildRows(IQueryable<OrderRow> rows) =>
        rows.Include(row => row.Lines);

    protected override OrderRow ToRow(Order aggregate) =>
        new()
        {
            Id = aggregate.Id,
            Name = aggregate.Name,
            Description = aggregate.Description,
            OrderDate = aggregate.OrderDate,
            Lines =
            [
                .. aggregate.Lines.Select((line, position) => new OrderLineRow
                {
                    OrderId = aggregate.Id,
                    BoardId = line.BoardId,
                    Position = position,
                    Quantity = line.Quantity,
                }),
            ],
            Status = ToStoredStatus(aggregate.Status),
            DownloadedAt = aggregate.DownloadedAt,
        };

    protected override Order ToAggregate(OrderRow row) =>
        Order.Restore(
            row.Id,
            row.Name,
            row.Description,
            row.OrderDate,
            row.Lines
                .OrderBy(line => line.Position)
                .Select(line => new OrderLine(line.BoardId, line.Quantity)),
            ToDomainStatus(row),
            row.DownloadedAt);

    private static string ToStoredStatus(OrderStatus status) => status switch
    {
        OrderStatus.Draft => DraftStatus,
        OrderStatus.Downloaded => DownloadedStatus,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The order status has no stored form."),
    };

    private static OrderStatus ToDomainStatus(OrderRow row) => row.Status switch
    {
        DraftStatus => OrderStatus.Draft,
        DownloadedStatus => OrderStatus.Downloaded,
        _ => throw new InvalidDataException(
            $"The stored order {row.Id} in the SQLite database has the unknown status '{row.Status}'."),
    };
}
