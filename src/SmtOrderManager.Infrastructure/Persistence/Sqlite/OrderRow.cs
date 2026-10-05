namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// A row of the <c>Orders</c> table, with the rows of its order lines.
/// </summary>
internal sealed class OrderRow : IRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTimeOffset OrderDate { get; init; }

    public List<OrderLineRow> Lines { get; init; } = [];

    /// <summary>
    /// The status as text, see <see cref="SqliteOrderRepository"/> for the stored values.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset? DownloadedAt { get; init; }
}

/// <summary>
/// A row of the <c>OrderLines</c> table. <see cref="Position"/> keeps the order of the lines.
/// </summary>
internal sealed class OrderLineRow
{
    public Guid OrderId { get; init; }

    public Guid BoardId { get; init; }

    public int Position { get; init; }

    public int Quantity { get; init; }
}
