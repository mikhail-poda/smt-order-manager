namespace SmtOrderManager.Application.Orders;

/// <summary>
/// Request to create a production order. New orders are always drafts.
/// </summary>
/// <param name="Name">The order name.</param>
/// <param name="Description">An optional description.</param>
/// <param name="OrderDate">The point in time at which the order is issued.</param>
/// <param name="Lines">The boards to produce; each board at most once.</param>
public sealed record CreateOrderCommand(
    string Name,
    string? Description,
    DateTimeOffset OrderDate,
    IReadOnlyList<OrderLineInput> Lines);
