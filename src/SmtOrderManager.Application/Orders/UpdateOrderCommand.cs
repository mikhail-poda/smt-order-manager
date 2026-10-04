namespace SmtOrderManager.Application.Orders;

/// <summary>
/// Request to change a draft order. All values replace the current ones, including all order
/// lines.
/// </summary>
/// <param name="Id">The order to change.</param>
/// <param name="Name">The new name.</param>
/// <param name="Description">The new description.</param>
/// <param name="OrderDate">The new order date.</param>
/// <param name="Lines">The new order lines; each board at most once.</param>
public sealed record UpdateOrderCommand(
    Guid Id,
    string Name,
    string? Description,
    DateTimeOffset OrderDate,
    IReadOnlyList<OrderLineInput> Lines);
