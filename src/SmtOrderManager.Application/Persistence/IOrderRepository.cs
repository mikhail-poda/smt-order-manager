using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Persistence;

/// <summary>
/// Repository for production orders.
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    /// <summary>
    /// Finds the orders that contain an order line for at least one of the boards. Used to
    /// enforce restricted deletion of boards.
    /// </summary>
    Task<IReadOnlyList<Order>> FindOrdersUsingBoardsAsync(
        IReadOnlyCollection<Guid> boardIds,
        CancellationToken cancellationToken = default);
}
