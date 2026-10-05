namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// The stored form of an order with its lines and status.
/// </summary>
internal sealed record OrderDocument(
    Guid Id,
    string Name,
    string Description,
    DateTimeOffset OrderDate,
    IReadOnlyList<OrderLineDocument> Lines,
    StoredOrderStatus Status,
    DateTimeOffset? DownloadedAt) : IDocument;

/// <summary>
/// The stored form of an order line.
/// </summary>
internal sealed record OrderLineDocument(Guid BoardId, int Quantity);

/// <summary>
/// The stored order status. It is a separate type from the domain enum, so renaming a domain
/// value cannot silently change the meaning of data that is already stored.
/// </summary>
internal enum StoredOrderStatus
{
    Draft,
    Downloaded,
}
