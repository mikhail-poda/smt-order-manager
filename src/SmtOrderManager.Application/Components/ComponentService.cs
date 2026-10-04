using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;
using SmtOrderManager.Application.Common;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Application.Components;

/// <summary>
/// Use cases for the component library. Create, update and remove accept one or more items;
/// a batch is saved only if every item is valid, otherwise all violations are returned and
/// nothing is changed.
/// </summary>
public sealed partial class ComponentService
{
    private readonly IComponentRepository _components;
    private readonly ILogger<ComponentService> _logger;

    public ComponentService(IComponentRepository components, ILogger<ComponentService> logger)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(logger);

        _components = components;
        _logger = logger;
    }

    public async Task<OperationResult<IReadOnlyList<ComponentDetails>>> CreateAsync(
        IReadOnlyCollection<CreateComponentCommand> commands,
        CancellationToken cancellationToken = default)
    {
        EnsureNotEmpty(commands);

        var violations = new ViolationCollector();
        var created = new List<Component>();
        var position = 0;

        foreach (var command in commands)
        {
            position++;
            var component = violations.TryCreate(
                $"Item {position}",
                () => Component.Create(command.Name, command.Description));

            if (component is not null)
            {
                created.Add(component);
            }
        }

        if (violations.HasViolations)
        {
            return Reject<IReadOnlyList<ComponentDetails>>("creation", commands.Count, violations);
        }

        await _components.SaveAsync(created, cancellationToken);
        LogSucceeded("Created", created);

        return Success(created);
    }

    public async Task<OperationResult<IReadOnlyList<ComponentDetails>>> UpdateAsync(
        IReadOnlyCollection<UpdateComponentCommand> commands,
        CancellationToken cancellationToken = default)
    {
        EnsureNotEmpty(commands);

        var violations = new ViolationCollector();
        var existing = await LoadAsync(commands.Select(command => command.Id), cancellationToken);
        var duplicateIds = commands
            .GroupBy(command => command.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        foreach (var id in duplicateIds)
        {
            violations.Add(Target(id, existing), "Component appears more than once in the batch.");
        }

        var updated = new List<Component>();

        foreach (var command in commands.Where(command => !duplicateIds.Contains(command.Id)))
        {
            if (!existing.TryGetValue(command.Id, out var component))
            {
                violations.Add(Target(command.Id, existing), "Component not found.");
                continue;
            }

            var target = Target(command.Id, existing);
            var changed = violations.TryApply(target, () =>
            {
                component.Rename(command.Name);
                component.ChangeDescription(command.Description);
            });

            if (changed)
            {
                updated.Add(component);
            }
        }

        if (violations.HasViolations)
        {
            return Reject<IReadOnlyList<ComponentDetails>>("update", commands.Count, violations);
        }

        await _components.SaveAsync(updated, cancellationToken);
        LogSucceeded("Updated", updated);

        return Success(updated);
    }

    /// <summary>
    /// Finds components whose name or description contains the text, ignoring case.
    /// </summary>
    /// <param name="text"><see langword="null"/> or blank returns all components.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching components, sorted by name.</returns>
    public async Task<IReadOnlyList<ComponentDetails>> SearchAsync(
        string? text,
        CancellationToken cancellationToken = default)
    {
        var components = await _components.SearchAsync(text, cancellationToken);

        return components
            .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ComponentDetails.From)
            .ToList()
            .AsReadOnly();
    }

    /// <returns>The number of removed components.</returns>
    public async Task<OperationResult<int>> RemoveAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        EnsureNotEmpty(ids);

        var distinctIds = ids.Distinct().ToList();
        var violations = new ViolationCollector();
        var existing = await LoadAsync(distinctIds, cancellationToken);

        foreach (var id in distinctIds.Where(id => !existing.ContainsKey(id)))
        {
            violations.Add(Target(id, existing), "Component not found.");
        }

        if (violations.HasViolations)
        {
            return Reject<int>("removal", ids.Count, violations);
        }

        var removed = distinctIds.ConvertAll(id => existing[id]);

        await _components.RemoveAsync(distinctIds, cancellationToken);
        LogSucceeded("Removed", removed);

        return OperationResult.Success(distinctIds.Count);
    }

    private async Task<Dictionary<Guid, Component>> LoadAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var components = await _components.GetByIdsAsync(ids.Distinct().ToList(), cancellationToken);

        return components.ToDictionary(component => component.Id);
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
    private void LogSucceeded(string operation, List<Component> components)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogSucceeded(_logger, operation, components.Count, components.ConvertAll(component => component.Name));
        }
    }

    private static string Target(Guid id, Dictionary<Guid, Component> existing) =>
        existing.TryGetValue(id, out var component) ? $"Component '{component.Name}'" : $"Component {id}";

    private static OperationResult<IReadOnlyList<ComponentDetails>> Success(List<Component> components) =>
        OperationResult.Success<IReadOnlyList<ComponentDetails>>(
            components.ConvertAll(ComponentDetails.From).AsReadOnly());

    private static void EnsureNotEmpty<T>(IReadOnlyCollection<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
        {
            throw new ArgumentException("A batch must contain at least one item.", nameof(items));
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        SkipEnabledCheck = true,
        Message = "{Operation} {Count} components: {Names}")]
    private static partial void LogSucceeded(ILogger logger, string operation, int count, List<string> names);

    [LoggerMessage(
        Level = LogLevel.Warning,
        SkipEnabledCheck = true,
        Message = "Rejected component {Operation} of {ItemCount} items with {ViolationCount} violations: {Violations}")]
    private static partial void LogRejected(
        ILogger logger,
        string operation,
        int itemCount,
        int violationCount,
        string violations);
}