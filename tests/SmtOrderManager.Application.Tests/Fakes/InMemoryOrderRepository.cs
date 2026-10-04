using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Tests.Fakes;

internal sealed class InMemoryOrderRepository : InMemoryRepository<Order>, IOrderRepository
{
    public Task<IReadOnlyList<Order>> FindOrdersUsingBoardsAsync(
        IReadOnlyCollection<Guid> boardIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(boardIds);

        return Task.FromResult(Query(order => order.Lines.Any(line => boardIds.Contains(line.BoardId))));
    }

    protected override Order Copy(Order aggregate) =>
        Order.Restore(
            aggregate.Id,
            aggregate.Name,
            aggregate.Description,
            aggregate.OrderDate,
            aggregate.Lines,
            aggregate.Status,
            aggregate.DownloadedAt);

    protected override IEnumerable<string> SearchableTexts(Order aggregate) =>
        [aggregate.Name, aggregate.Description];
}
