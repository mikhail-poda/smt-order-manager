using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Application.Boards;

/// <summary>
/// Use cases for board designs and their bills of materials. Create, update and remove accept
/// one or more items; a batch is saved only if every item is valid, otherwise all violations
/// are returned and nothing is changed.
/// </summary>
/// <remarks>
/// Rules that need other aggregates are checked here, because a board cannot see components:
/// every referenced component must exist. Violations name components instead of showing their
/// identifiers where the component is known. A board that is still used in an order cannot be
/// removed; every such order is reported.
/// </remarks>
public sealed partial class BoardService
{
    private readonly IBoardRepository _boards;
    private readonly IComponentRepository _components;
    private readonly IOrderRepository _orders;
    private readonly ILogger<BoardService> _logger;

    public BoardService(
        IBoardRepository boards,
        IComponentRepository components,
        IOrderRepository orders,
        ILogger<BoardService> logger)
    {
        ArgumentNullException.ThrowIfNull(boards);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(logger);

        _boards = boards;
        _components = components;
        _orders = orders;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<BoardDetails>>> CreateAsync(
        IReadOnlyCollection<CreateBoardCommand> commands,
        CancellationToken cancellationToken = default)
    {
        Batch.EnsureNotEmpty(commands);

        var componentNames = await LoadComponentNamesAsync(
            commands.SelectMany(command => command.BillOfMaterials).Select(input => input.ComponentId),
            cancellationToken);
        var violations = new ViolationCollector();
        var created = new List<Board>();
        var position = 0;

        foreach (var command in commands)
        {
            position++;
            var target = $"Item {position}";
            var entries = ToBomEntries(target, command.BillOfMaterials, componentNames, violations);

            if (entries is null)
            {
                continue;
            }

            var board = violations.TryCreate(
                target,
                () => Board.Create(command.Name, command.Description, command.Length, command.Width, entries));

            if (board is not null)
            {
                created.Add(board);
            }
        }

        if (violations.HasViolations)
        {
            return Reject<IReadOnlyList<BoardDetails>>("creation", commands.Count, violations);
        }

        await _boards.SaveAsync(created, cancellationToken);
        LogSucceeded("Created", created);

        return Success(created, componentNames);
    }

    public async Task<OperationResult<IReadOnlyList<BoardDetails>>> UpdateAsync(
        IReadOnlyCollection<UpdateBoardCommand> commands,
        CancellationToken cancellationToken = default)
    {
        Batch.EnsureNotEmpty(commands);

        var existing = await LoadAsync(commands.Select(command => command.Id), cancellationToken);
        var componentNames = await LoadComponentNamesAsync(
            commands.SelectMany(command => command.BillOfMaterials).Select(input => input.ComponentId),
            cancellationToken);
        var violations = new ViolationCollector();
        var duplicateIds = Batch.FindDuplicates(commands, command => command.Id);

        foreach (var id in duplicateIds)
        {
            violations.Add(Target(id, existing), "Board appears more than once in the batch.");
        }

        var updated = new List<Board>();

        foreach (var command in commands.Where(command => !duplicateIds.Contains(command.Id)))
        {
            var target = Target(command.Id, existing);

            if (!existing.TryGetValue(command.Id, out var board))
            {
                violations.Add(target, "Board not found.");
                continue;
            }

            // Each change is tried separately, so all problems of one board are reported.
            var entries = ToBomEntries(target, command.BillOfMaterials, componentNames, violations);
            violations.TryApply(target, () => board.Rename(command.Name));
            violations.TryApply(target, () => board.ChangeDescription(command.Description));
            violations.TryApply(target, () => board.ChangeDimensions(command.Length, command.Width));

            if (entries is not null)
            {
                violations.TryApply(target, () => ReplaceBillOfMaterials(board, entries));
            }

            updated.Add(board);
        }

        if (violations.HasViolations)
        {
            return Reject<IReadOnlyList<BoardDetails>>("update", commands.Count, violations);
        }

        await _boards.SaveAsync(updated, cancellationToken);
        LogSucceeded("Updated", updated);

        return Success(updated, componentNames);
    }

    /// <summary>
    /// Finds boards whose name or description contains the text, ignoring case.
    /// </summary>
    /// <param name="text"><see langword="null"/> or blank returns all boards.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching boards, sorted by name.</returns>
    public async Task<IReadOnlyList<BoardDetails>> SearchAsync(
        string? text,
        CancellationToken cancellationToken = default)
    {
        var boards = await _boards.SearchAsync(text, cancellationToken);
        var componentNames = await LoadComponentNamesAsync(
            boards.SelectMany(board => board.BillOfMaterials).Select(entry => entry.ComponentId),
            cancellationToken);

        return boards
            .OrderBy(board => board.Name, StringComparer.OrdinalIgnoreCase)
            .Select(board => BoardDetails.From(board, componentNames))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Removes boards that no order uses. If any board in the batch is unknown or still used,
    /// nothing is removed.
    /// </summary>
    /// <returns>The number of removed boards.</returns>
    public async Task<OperationResult<int>> RemoveAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        Batch.EnsureNotEmpty(ids);

        var distinctIds = ids.Distinct().ToList();
        var violations = new ViolationCollector();
        var existing = await LoadAsync(distinctIds, cancellationToken);

        foreach (var id in distinctIds.Where(id => !existing.ContainsKey(id)))
        {
            violations.Add(Target(id, existing), "Board not found.");
        }

        var orders = await _orders.FindOrdersUsingBoardsAsync(existing.Keys, cancellationToken);

        foreach (var id in distinctIds.Where(existing.ContainsKey))
        {
            var usages = orders
                .Where(order => order.Lines.Any(line => line.BoardId == id))
                .Select(order => order.Name)
                .Order(StringComparer.OrdinalIgnoreCase);

            foreach (var orderName in usages)
            {
                violations.Add(Target(id, existing), $"Board is used by order '{orderName}'.");
            }
        }

        if (violations.HasViolations)
        {
            return Reject<int>("removal", ids.Count, violations);
        }

        var removed = distinctIds.ConvertAll(id => existing[id]);

        await _boards.RemoveAsync(distinctIds, cancellationToken);
        LogSucceeded("Removed", removed);

        return OperationResult.Success(distinctIds.Count);
    }

    /// <summary>
    /// Converts the requested BOM and checks the rules that need more than one entry or other
    /// aggregates: not empty, each component at most once, every component exists.
    /// </summary>
    /// <returns>The entries, or <see langword="null"/> when any rule is violated.</returns>
    private static List<BomEntry>? ToBomEntries(
        string target,
        IReadOnlyList<BomEntryInput> inputs,
        Dictionary<Guid, string> componentNames,
        ViolationCollector violations)
    {
        if (inputs.Count == 0)
        {
            violations.Add(target, "A board must contain at least one BOM entry.");
            return null;
        }

        var violationsBefore = violations.Count;
        var entries = new List<BomEntry>();

        foreach (var input in inputs)
        {
            var entry = violations.TryCreate(target, () => new BomEntry(input.ComponentId, input.Quantity));

            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        foreach (var componentId in Batch.FindDuplicates(inputs, input => input.ComponentId))
        {
            violations.Add(
                target,
                $"{ComponentLabel(componentId, componentNames)} appears more than once in the bill of materials.");
        }

        var missing = inputs
            .Select(input => input.ComponentId)
            .Where(componentId => componentId != Guid.Empty && !componentNames.ContainsKey(componentId))
            .Distinct();

        foreach (var componentId in missing)
        {
            violations.Add(target, $"Component {componentId} not found.");
        }

        return violations.Count == violationsBefore ? entries : null;
    }

    /// <summary>
    /// Makes the board's bill of materials equal to the entries. New entries are set first, so
    /// the board never runs empty while obsolete entries are removed. Retained entries keep their
    /// position; new entries are appended.
    /// </summary>
    private static void ReplaceBillOfMaterials(Board board, List<BomEntry> entries)
    {
        foreach (var entry in entries)
        {
            board.SetBomEntry(entry.ComponentId, entry.Quantity);
        }

        var requested = entries.Select(entry => entry.ComponentId).ToHashSet();
        var obsolete = board.BillOfMaterials
            .Select(entry => entry.ComponentId)
            .Where(componentId => !requested.Contains(componentId))
            .ToList();

        foreach (var componentId in obsolete)
        {
            board.RemoveBomEntry(componentId);
        }
    }

    private async Task<Dictionary<Guid, Board>> LoadAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var boards = await _boards.GetByIdsAsync(ids.Distinct().ToList(), cancellationToken);

        return boards.ToDictionary(board => board.Id);
    }

    private async Task<Dictionary<Guid, string>> LoadComponentNamesAsync(
        IEnumerable<Guid> componentIds,
        CancellationToken cancellationToken)
    {
        var ids = componentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var components = await _components.GetByIdsAsync(ids, cancellationToken);

        return components.ToDictionary(component => component.Id, component => component.Name);
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
    private void LogSucceeded(string operation, List<Board> boards)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogSucceeded(_logger, operation, boards.Count, boards.ConvertAll(board => board.Name));
        }
    }

    private static OperationResult<IReadOnlyList<BoardDetails>> Success(
        List<Board> boards,
        Dictionary<Guid, string> componentNames) =>
        OperationResult.Success<IReadOnlyList<BoardDetails>>(
            boards.ConvertAll(board => BoardDetails.From(board, componentNames)).AsReadOnly());

    private static string Target(Guid id, Dictionary<Guid, Board> existing) =>
        existing.TryGetValue(id, out var board) ? $"Board '{board.Name}'" : $"Board {id}";

    private static string ComponentLabel(Guid componentId, Dictionary<Guid, string> componentNames) =>
        componentNames.TryGetValue(componentId, out var name) ? $"Component '{name}'" : $"Component {componentId}";

    [LoggerMessage(
        Level = LogLevel.Information,
        SkipEnabledCheck = true,
        Message = "{Operation} {Count} boards: {Names}")]
    private static partial void LogSucceeded(ILogger logger, string operation, int count, List<string> names);

    [LoggerMessage(
        Level = LogLevel.Warning,
        SkipEnabledCheck = true,
        Message = "Rejected board {Operation} of {ItemCount} items with {ViolationCount} violations: {Violations}")]
    private static partial void LogRejected(
        ILogger logger,
        string operation,
        int itemCount,
        int violationCount,
        string violations);
}