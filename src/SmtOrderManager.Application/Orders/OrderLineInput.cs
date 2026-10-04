namespace SmtOrderManager.Application.Orders;

/// <summary>
/// One board of an order in a create or update request.
/// </summary>
/// <param name="BoardId">The board, by identifier.</param>
/// <param name="Quantity">The number of boards to produce.</param>
public sealed record OrderLineInput(Guid BoardId, int Quantity);
