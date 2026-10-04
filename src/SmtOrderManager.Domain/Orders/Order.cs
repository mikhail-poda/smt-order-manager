using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Orders;

/// <summary>
/// A production order: a production job that states which boards to build and how many of
/// each. "Order" always means a production order, never a customer order.
/// </summary>
/// <remarks>
/// An order starts as a draft. Once an SMT line has accepted it, it is downloaded and can no
/// longer be edited, because the line would otherwise produce something other than what the
/// system shows.
/// </remarks>
public sealed class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines;

    private Order(
        Guid id,
        string name,
        string? description,
        DateTimeOffset orderDate,
        IEnumerable<OrderLine> lines,
        OrderStatus status,
        DateTimeOffset? downloadedAt)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Name = Guard.NotBlank(name, nameof(Name));
        Description = NormalizeDescription(description);
        OrderDate = ValidateTimestamp(orderDate, nameof(OrderDate));
        _lines = ValidateLines(lines);
        (Status, DownloadedAt) = ValidateStatus(status, downloadedAt);
    }

    /// <summary>
    /// The descriptive name of the order. Never blank, without surrounding whitespace.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// An optional free-text description. Empty when no description is given.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// The point in time at which the order was issued, with its offset from UTC.
    /// </summary>
    public DateTimeOffset OrderDate { get; private set; }

    /// <summary>
    /// The boards to produce. Contains at least one line and each board at most once. The
    /// list is read-only; changes go through <see cref="SetLine"/> and <see cref="RemoveLine"/>.
    /// </summary>
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>
    /// The lifecycle state of the order.
    /// </summary>
    public OrderStatus Status { get; private set; }

    /// <summary>
    /// The point in time of the most recent accepted download. Set exactly when the order is
    /// downloaded.
    /// </summary>
    public DateTimeOffset? DownloadedAt { get; private set; }

    /// <summary>
    /// Whether the order can still be edited and removed.
    /// </summary>
    public bool IsEditable => Status == OrderStatus.Draft;

    /// <summary>
    /// Creates a new draft order with a newly generated identifier.
    /// </summary>
    public static Order Create(
        string name,
        string? description,
        DateTimeOffset orderDate,
        IEnumerable<OrderLine> lines) =>
        new(Guid.NewGuid(), name, description, orderDate, lines, OrderStatus.Draft, downloadedAt: null);

    /// <summary>
    /// Rebuilds a persisted order with its existing identifier and status. The same rules apply
    /// as for a new order, so invalid stored data is detected when it is loaded.
    /// </summary>
    public static Order Restore(
        Guid id,
        string name,
        string? description,
        DateTimeOffset orderDate,
        IEnumerable<OrderLine> lines,
        OrderStatus status,
        DateTimeOffset? downloadedAt) =>
        new(id, name, description, orderDate, lines, status, downloadedAt);

    public void Rename(string name)
    {
        EnsureEditable();
        Name = Guard.NotBlank(name, nameof(Name));
    }

    public void ChangeDescription(string? description)
    {
        EnsureEditable();
        Description = NormalizeDescription(description);
    }

    public void ChangeOrderDate(DateTimeOffset orderDate)
    {
        EnsureEditable();
        OrderDate = ValidateTimestamp(orderDate, nameof(OrderDate));
    }

    /// <summary>
    /// Adds an order line for the board, or replaces the existing line for that board with a
    /// new one. A replaced line keeps its position in the list.
    /// </summary>
    public void SetLine(Guid boardId, int quantity)
    {
        EnsureEditable();

        var line = new OrderLine(boardId, quantity);
        var index = IndexOf(boardId);

        if (index >= 0)
        {
            _lines[index] = line;
        }
        else
        {
            _lines.Add(line);
        }
    }

    /// <summary>
    /// Removes the order line for the board. The last line cannot be removed, because an
    /// order without boards has nothing to produce.
    /// </summary>
    public void RemoveLine(Guid boardId)
    {
        EnsureEditable();

        var index = IndexOf(boardId);

        if (index < 0)
        {
            throw new DomainException($"Board {boardId} is not part of order '{Name}'.");
        }

        if (_lines.Count == 1)
        {
            throw new DomainException($"The last order line of order '{Name}' cannot be removed.");
        }

        _lines.RemoveAt(index);
    }

    /// <summary>
    /// Records that an SMT line has accepted the order. An order can be downloaded more than
    /// once, for example when the line needs the job again; <see cref="DownloadedAt"/> then
    /// holds the time of the most recent download.
    /// </summary>
    public void MarkDownloaded(DateTimeOffset downloadedAt)
    {
        DownloadedAt = ValidateTimestamp(downloadedAt, nameof(DownloadedAt));
        Status = OrderStatus.Downloaded;
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new DomainException($"Order '{Name}' has been downloaded and can no longer be changed.");
        }
    }

    private int IndexOf(Guid boardId) => _lines.FindIndex(line => line.BoardId == boardId);

    private static DateTimeOffset ValidateTimestamp(DateTimeOffset value, string name)
    {
        if (value == default)
        {
            throw new DomainException($"{name} must be set.");
        }

        return value;
    }

    private static (OrderStatus Status, DateTimeOffset? DownloadedAt) ValidateStatus(
        OrderStatus status,
        DateTimeOffset? downloadedAt)
    {
        return status switch
        {
            OrderStatus.Draft when downloadedAt is not null =>
                throw new DomainException("A draft order must not have a download time."),
            OrderStatus.Draft => (status, null),
            OrderStatus.Downloaded when downloadedAt is null =>
                throw new DomainException("A downloaded order must have a download time."),
            OrderStatus.Downloaded => (status, ValidateTimestamp(downloadedAt.Value, nameof(DownloadedAt))),
            _ => throw new DomainException($"Unknown order status: {status}."),
        };
    }

    private static List<OrderLine> ValidateLines(IEnumerable<OrderLine> lines)
    {
        var validLines = lines.ToList();

        if (validLines.Count == 0)
        {
            throw new DomainException("An order must contain at least one order line.");
        }

        var duplicates = validLines
            .GroupBy(line => line.BoardId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new DomainException(
                $"Each board may appear only once in an order. Duplicates: {string.Join(", ", duplicates)}.");
        }

        return validLines;
    }

    private static string NormalizeDescription(string? description) => description?.Trim() ?? string.Empty;
}