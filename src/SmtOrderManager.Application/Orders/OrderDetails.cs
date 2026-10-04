using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Orders;

/// <summary>
/// Read model of an order for callers of the application layer, with board names resolved for
/// display.
/// </summary>
public sealed record OrderDetails(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset OrderDate,
    OrderStatus Status,
    DateTimeOffset? DownloadedAt,
    IReadOnlyList<OrderLineDetails> Lines)
{
    internal static OrderDetails From(Order order, Dictionary<Guid, string> boardNames) =>
        new(
            order.Id,
            order.Name,
            order.Description,
            order.OrderDate,
            order.Status,
            order.DownloadedAt,
            order.Lines
                .Select(line => new OrderLineDetails(
                    line.BoardId,
                    boardNames.GetValueOrDefault(line.BoardId, OrderLineDetails.UnknownBoardName),
                    line.Quantity))
                .ToList()
                .AsReadOnly());
}

/// <summary>
/// Read model of one order line.
/// </summary>
public sealed record OrderLineDetails(Guid BoardId, string BoardName, int Quantity)
{
    /// <summary>
    /// Shown when a referenced board no longer exists. Restricted deletion prevents this, so it
    /// only appears with inconsistent stored data.
    /// </summary>
    public const string UnknownBoardName = "(unknown board)";
}
