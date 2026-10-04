using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Application.Orders;

/// <summary>
/// Use cases for production orders. Create, update and remove accept one or more items; a batch
/// is saved only if every item is valid, otherwise all violations are returned and nothing is
/// changed.
/// </summary>
/// <remarks>
/// Rules that need other aggregates or are not operations of the aggregate are checked here:
/// every referenced board must exist, and a downloaded order can neither be changed nor
/// removed. The <see cref="Order"/> aggregate blocks changes itself; removal is not one of its
/// operations, so this service blocks it.
/// </remarks>
public sealed partial class OrderService
{
    private readonly IOrderRepository _orders;
    private readonly IBoardRepository _boards;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository orders, IBoardRepository boards, ILogger<OrderService> logger)
    {
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(boards);
        ArgumentNullException.ThrowIfNull(logger);

        _orders = orders;
        _boards = boards;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<OrderDetails>>> CreateAsync(
        IReadOnlyCollection<CreateOrderCommand> commands,
        CancellationToken cancellationToken = default)
    {
        Batch.EnsureNotEmpty(commands);

        var boardNames = await LoadBoardNamesAsync(
            commands.SelectMany(command => command.Lines).Select(input => input.BoardId),
            cancellationToken);
        var violations = new ViolationCollector();
        var created = new List<Order>();
        var position = 0;

        foreach (var command in commands)
        {
            position++;
            var target = $"Item {position}";
            var lines = ToOrderLines(target, command.Lines, boardNames, violations);

            if (lines is null)
            {
                continue;
            }

            var order = violations.TryCreate(
                target,
                () => Order.Create(command.Name, command.Description, command.OrderDate, lines));

            if (order is not null)
            {
                created.Add(order);
            }
        }

        if (violations.HasViolations)
        {
            return Reject<IReadOnlyList<OrderDetails>>("creation", commands.Count, violations);
        }

        await _orders.SaveAsync(created, cancellationToken);
        LogSucceeded("Created", created);

        return Success(created, boardNames);
    }

    public async Task<OperationResult<IReadOnlyList<OrderDetails>>> UpdateAsync(
        IReadOnlyCollection<UpdateOrderCommand> commands,
        CancellationToken cancellationToken = default)
    {
        Batch.EnsureNotEmpty(commands);

        var existing = await LoadAsync(commands.Select(command => command.Id), cancellationToken);
        var boardNames = await LoadBoardNamesAsync(
            commands.SelectMany(command => command.Lines).Select(input => input.BoardId),
            cancellationToken);
        var violations = new ViolationCollector();
        var duplicateIds = Batch.FindDuplicates(commands, command => command.Id);

        foreach (var id in duplicateIds)
        {
            violations.Add(Target(id, existing), "Order appears more than once in the batch.");
        }

        var updated = new List<Order>();

        foreach (var command in commands.Where(command => !duplicateIds.Contains(command.Id)))
        {
            var target = Target(command.Id, existing);

            if (!existing.TryGetValue(command.Id, out var order))
            {
                violations.Add(target, "Order not found.");
                continue;
            }

            // Checked once here; otherwise every single change would report the same rule.
            if (!order.IsEditable)
            {
                violations.Add(target, "Order has been downloaded and can no longer be changed.");
                continue;
            }

            // Each change is tried separately, so all problems of one order are reported.
            var lines = ToOrderLines(target, command.Lines, boardNames, violations);
            violations.TryApply(target, () => order.Rename(command.Name));
            violations.TryApply(target, () => order.ChangeDescription(command.Description));
            violations.TryApply(target, () => order.ChangeOrderDate(command.OrderDate));

            if (lines is not null)
            {
                violations.TryApply(target, () => ReplaceLines(order, lines));
            }

            updated.Add(order);
        }

        if (violations.HasViolations)
        {
            return Reject<IReadOnlyList<OrderDetails>>("update", commands.Count, violations);
        }

        await _orders.SaveAsync(updated, cancellationToken);
        LogSucceeded("Updated", updated);

        return Success(updated, boardNames);
    }

    /// <summary>
    /// Finds orders whose name or description contains the text, ignoring case.
    /// </summary>
    /// <param name="text"><see langword="null"/> or blank returns all orders.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching orders, newest order date first, then by name.</returns>
    public async Task<IReadOnlyList<OrderDetails>> SearchAsync(
        string? text,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orders.SearchAsync(text, cancellationToken);
        var boardNames = await LoadBoardNamesAsync(
            orders.SelectMany(order => order.Lines).Select(line => line.BoardId),
            cancellationToken);

        return orders
            .OrderByDescending(order => order.OrderDate)
            .ThenBy(order => order.Name, StringComparer.OrdinalIgnoreCase)
            .Select(order => OrderDetails.From(order, boardNames))
            .ToList()
            .AsReadOnly();
    }

    /// <returns>The number of removed orders.</returns>
    public async Task<OperationResult<int>> RemoveAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        Batch.EnsureNotEmpty(ids);

        var distinctIds = ids.Distinct().ToList();
        var violations = new ViolationCollector();
        var existing = await LoadAsync(distinctIds, cancellationToken);

        foreach (var id in distinctIds)
        {
            if (!existing.TryGetValue(id, out var order))
            {
                violations.Add(Target(id, existing), "Order not found.");
            }
            else if (!order.IsEditable)
            {
                violations.Add(Target(id, existing), "Order has been downloaded and can no longer be removed.");
            }
        }

        if (violations.HasViolations)
        {
            return Reject<int>("removal", ids.Count, violations);
        }

        var removed = distinctIds.ConvertAll(id => existing[id]);

        await _orders.RemoveAsync(distinctIds, cancellationToken);
        LogSucceeded("Removed", removed);

        return OperationResult.Success(distinctIds.Count);
    }

    /// <summary>
    /// Converts the requested lines and checks the rules that need more than one line or other
    /// aggregates: not empty, each board at most once, every board exists.
    /// </summary>
    /// <returns>The lines, or <see langword="null"/> when any rule is violated.</returns>
    private static List<OrderLine>? ToOrderLines(
        string target,
        IReadOnlyList<OrderLineInput> inputs,
        Dictionary<Guid, string> boardNames,
        ViolationCollector violations)
    {
        if (inputs.Count == 0)
        {
            violations.Add(target, "An order must contain at least one order line.");
            return null;
        }

        var violationsBefore = violations.Count;
        var lines = new List<OrderLine>();

        foreach (var input in inputs)
        {
            var line = violations.TryCreate(target, () => new OrderLine(input.BoardId, input.Quantity));

            if (line is not null)
            {
                lines.Add(line);
            }
        }

        foreach (var boardId in Batch.FindDuplicates(inputs, input => input.BoardId))
        {
            violations.Add(target, $"{BoardLabel(boardId, boardNames)} appears more than once in the order.");
        }

        var missing = inputs
            .Select(input => input.BoardId)
            .Where(boardId => boardId != Guid.Empty && !boardNames.ContainsKey(boardId))
            .Distinct();

        foreach (var boardId in missing)
        {
            violations.Add(target, $"Board {boardId} not found.");
        }

        return violations.Count == violationsBefore ? lines : null;
    }

    /// <summary>
    /// Makes the order's lines equal to the requested lines. New lines are set first, so the
    /// order never runs empty while obsolete lines are removed. Retained lines keep their
    /// position; new lines are appended.
    /// </summary>
    private static void ReplaceLines(Order order, List<OrderLine> lines)
    {
        foreach (var line in lines)
        {
            order.SetLine(line.BoardId, line.Quantity);
        }

        var requested = lines.Select(line => line.BoardId).ToHashSet();
        var obsolete = order.Lines
            .Select(line => line.BoardId)
            .Where(boardId => !requested.Contains(boardId))
            .ToList();

        foreach (var boardId in obsolete)
        {
            order.RemoveLine(boardId);
        }
    }

    private async Task<Dictionary<Guid, Order>> LoadAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var orders = await _orders.GetByIdsAsync(ids.Distinct().ToList(), cancellationToken);

        return orders.ToDictionary(order => order.Id);
    }

    private async Task<Dictionary<Guid, string>> LoadBoardNamesAsync(
        IEnumerable<Guid> boardIds,
        CancellationToken cancellationToken)
    {
        var ids = boardIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var boards = await _boards.GetByIdsAsync(ids, cancellationToken);

        return boards.ToDictionary(board => board.Id, board => board.Name);
    }

    private OperationResult<T> Reject<T>(string operation, int itemCount, ViolationCollector violations)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            LogRejected(_logger, operation, itemCount, violations.Count, violations.ToString());
        }

        return OperationResult.Failure<T>(violations.Violations);
    }

    // The names are only collected when the level is enabled (CA1873), so the generated
    // method skips its own check.
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging")]
    private void LogSucceeded(string operation, List<Order> orders)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogSucceeded(_logger, operation, orders.Count, orders.ConvertAll(order => order.Name));
        }
    }

    private static OperationResult<IReadOnlyList<OrderDetails>> Success(
        List<Order> orders,
        Dictionary<Guid, string> boardNames) =>
        OperationResult.Success<IReadOnlyList<OrderDetails>>(
            orders.ConvertAll(order => OrderDetails.From(order, boardNames)).AsReadOnly());

    private static string Target(Guid id, Dictionary<Guid, Order> existing) =>
        existing.TryGetValue(id, out var order) ? $"Order '{order.Name}'" : $"Order {id}";

    private static string BoardLabel(Guid boardId, Dictionary<Guid, string> boardNames) =>
        boardNames.TryGetValue(boardId, out var name) ? $"Board '{name}'" : $"Board {boardId}";

    [LoggerMessage(
        Level = LogLevel.Information,
        SkipEnabledCheck = true,
        Message = "{Operation} {Count} orders: {Names}")]
    private static partial void LogSucceeded(ILogger logger, string operation, int count, List<string> names);

    [LoggerMessage(
        Level = LogLevel.Warning,
        SkipEnabledCheck = true,
        Message = "Rejected order {Operation} of {ItemCount} items with {ViolationCount} violations: {Violations}")]
    private static partial void LogRejected(
        ILogger logger,
        string operation,
        int itemCount,
        int violationCount,
        string violations);
}
