using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Domain.Orders;

/// <summary>
/// A production order: a production job that states which boards to build and how many of
/// each. "Order" always means a production order, never a customer order.
/// </summary>
public sealed class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines;

    private Order(
        Guid id,
        string name,
        string? description,
        DateTimeOffset orderDate,
        IEnumerable<OrderLine> lines)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(lines);

        Name = Guard.NotBlank(name, nameof(Name));
        Description = NormalizeDescription(description);
        OrderDate = ValidateOrderDate(orderDate);
        _lines = ValidateLines(lines);
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
    /// Creates a new order with a newly generated identifier.
    /// </summary>
    public static Order Create(
        string name,
        string? description,
        DateTimeOffset orderDate,
        IEnumerable<OrderLine> lines) =>
        new(Guid.NewGuid(), name, description, orderDate, lines);

    /// <summary>
    /// Rebuilds a persisted order with its existing identifier. The same rules apply as for a
    /// new order, so invalid stored data is detected when it is loaded.
    /// </summary>
    public static Order Restore(
        Guid id,
        string name,
        string? description,
        DateTimeOffset orderDate,
        IEnumerable<OrderLine> lines) =>
        new(id, name, description, orderDate, lines);

    public void Rename(string name) => Name = Guard.NotBlank(name, nameof(Name));

    public void ChangeDescription(string? description) => Description = NormalizeDescription(description);

    public void ChangeOrderDate(DateTimeOffset orderDate) => OrderDate = ValidateOrderDate(orderDate);

    /// <summary>
    /// Adds an order line for the board, or replaces the existing line for that board with a
    /// new one. A replaced line keeps its position in the list.
    /// </summary>
    public void SetLine(Guid boardId, int quantity)
    {
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

    private int IndexOf(Guid boardId) => _lines.FindIndex(line => line.BoardId == boardId);

    private static DateTimeOffset ValidateOrderDate(DateTimeOffset orderDate)
    {
        if (orderDate == default)
        {
            throw new DomainException("OrderDate must be set.");
        }

        return orderDate;
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
