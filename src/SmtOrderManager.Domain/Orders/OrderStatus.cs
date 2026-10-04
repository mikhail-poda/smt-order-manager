namespace SmtOrderManager.Domain.Orders;

/// <summary>
/// The lifecycle state of an order.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// The order is being prepared and can be edited.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// An SMT line has accepted the order. It can no longer be edited or removed.
    /// </summary>
    Downloaded = 1,
}
