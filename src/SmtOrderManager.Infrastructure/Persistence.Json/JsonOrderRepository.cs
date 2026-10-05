using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// Stores orders with their lines and status in <see cref="FileName"/>.
/// </summary>
internal sealed class JsonOrderRepository(JsonFileStore<OrderDocument> store)
    : JsonRepository<Order, OrderDocument>(store), IOrderRepository
{
    public const string FileName = "orders.json";

    public Task<IReadOnlyList<Order>> FindOrdersUsingBoardsAsync(
        IReadOnlyCollection<Guid> boardIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(boardIds);

        var wanted = boardIds.ToHashSet();

        return FindAsync(
            document => document.Lines.Any(line => wanted.Contains(line.BoardId)),
            cancellationToken);
    }

    protected override OrderDocument ToDocument(Order aggregate) =>
        new(
            aggregate.Id,
            aggregate.Name,
            aggregate.Description,
            aggregate.OrderDate,
            [.. aggregate.Lines.Select(line => new OrderLineDocument(line.BoardId, line.Quantity))],
            ToStoredStatus(aggregate.Status),
            aggregate.DownloadedAt);

    protected override Order ToAggregate(OrderDocument document) =>
        Order.Restore(
            document.Id,
            document.Name,
            document.Description,
            document.OrderDate,
            document.Lines.Select(line => new OrderLine(line.BoardId, line.Quantity)),
            ToDomainStatus(document.Status),
            document.DownloadedAt);

    protected override IEnumerable<string> SearchableTexts(OrderDocument document) =>
        [document.Name, document.Description];

    private static StoredOrderStatus ToStoredStatus(OrderStatus status) => status switch
    {
        OrderStatus.Draft => StoredOrderStatus.Draft,
        OrderStatus.Downloaded => StoredOrderStatus.Downloaded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The order status has no stored form."),
    };

    private static OrderStatus ToDomainStatus(StoredOrderStatus status) => status switch
    {
        StoredOrderStatus.Draft => OrderStatus.Draft,
        StoredOrderStatus.Downloaded => OrderStatus.Downloaded,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "The stored order status is unknown."),
    };
}
