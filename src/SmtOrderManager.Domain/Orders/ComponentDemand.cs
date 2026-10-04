namespace SmtOrderManager.Domain.Orders;

/// <summary>
/// The total number of placements of one component required to produce an order.
/// </summary>
/// <param name="ComponentId">The component, by identifier.</param>
/// <param name="TotalQuantity">The total number of placements across all ordered boards.</param>
public sealed record ComponentDemand(Guid ComponentId, long TotalQuantity);
