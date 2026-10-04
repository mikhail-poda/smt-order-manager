using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Application.Tests.Fakes;

/// <summary>
/// In-memory implementation of <see cref="IRepository{TAggregate}"/> for application tests.
/// </summary>
/// <remarks>
/// The fake stores and returns copies, as a real store does after serialization. Without
/// copies, a test could change a loaded aggregate, skip <see cref="SaveAsync"/> and still see
/// the change in the store, which would hide a missing or skipped save in a use case.
/// </remarks>
internal abstract class InMemoryRepository<TAggregate> : IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    // Keeps insertion order, so results are deterministic.
    private readonly OrderedDictionary<Guid, TAggregate> _store = new();

    /// <summary>
    /// The number of stored aggregates.
    /// </summary>
    public int Count => _store.Count;

    /// <summary>
    /// Stores aggregates directly, for arranging a test.
    /// </summary>
    public void Seed(params TAggregate[] aggregates)
    {
        foreach (var aggregate in aggregates)
        {
            _store[aggregate.Id] = Copy(aggregate);
        }
    }

    public Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<TAggregate?>(_store.TryGetValue(id, out var aggregate) ? Copy(aggregate) : null);

    public Task<IReadOnlyList<TAggregate>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        IReadOnlyList<TAggregate> result =
        [
            .. ids
                .Distinct()
                .Where(_store.ContainsKey)
                .Select(id => Copy(_store[id]))
        ];

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<TAggregate>> SearchAsync(string? text, CancellationToken cancellationToken = default) =>
        Task.FromResult(Query(aggregate => Matches(aggregate, text)));

    public Task SaveAsync(IReadOnlyCollection<TAggregate> aggregates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        foreach (var aggregate in aggregates)
        {
            _store[aggregate.Id] = Copy(aggregate);
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        foreach (var id in ids)
        {
            _store.Remove(id);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns copies of all stored aggregates that match the predicate, in insertion order.
    /// </summary>
    protected IReadOnlyList<TAggregate> Query(Func<TAggregate, bool> predicate) =>
        [.. _store.Values.Where(predicate).Select(Copy)];

    /// <summary>
    /// Creates an independent copy of the aggregate, typically through its <c>Restore</c> factory.
    /// </summary>
    protected abstract TAggregate Copy(TAggregate aggregate);

    /// <summary>
    /// The texts that <see cref="SearchAsync"/> looks in: the name and the description.
    /// </summary>
    protected abstract IEnumerable<string> SearchableTexts(TAggregate aggregate);

    private bool Matches(TAggregate aggregate, string? text) =>
        string.IsNullOrWhiteSpace(text)
        || SearchableTexts(aggregate).Any(value => value.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase));
}
