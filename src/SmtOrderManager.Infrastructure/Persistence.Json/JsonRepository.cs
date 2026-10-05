using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// Implements <see cref="IRepository{TAggregate}"/> on top of a <see cref="JsonFileStore{TDocument}"/>.
/// Subclasses only map between the aggregate and its document.
/// </summary>
/// <remarks>
/// <para>
/// Every write is a single <see cref="JsonFileStore{TDocument}.UpdateAsync"/> call, so a batch is
/// written completely or not at all. Documents keep their position when they are replaced, and
/// new documents are appended, so results come back in a stable order.
/// </para>
/// <para>
/// The repository does not own the store. The store is a singleton shared by everything that
/// works with the same file, and the dependency injection container disposes it.
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TDocument">The stored form of the aggregate.</typeparam>
internal abstract class JsonRepository<TAggregate, TDocument>(JsonFileStore<TDocument> store) : IRepository<TAggregate>
    where TAggregate : AggregateRoot
    where TDocument : class, IDocument
{
    private readonly JsonFileStore<TDocument> _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var documents = await _store.LoadAsync(cancellationToken);
        var document = documents.FirstOrDefault(candidate => candidate.Id == id);

        return document is null ? null : Restore(document);
    }

    public Task<IReadOnlyList<TAggregate>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var wanted = ids.ToHashSet();

        return FindAsync(document => wanted.Contains(document.Id), cancellationToken);
    }

    public Task<IReadOnlyList<TAggregate>> SearchAsync(string? text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return FindAsync(_ => true, cancellationToken);
        }

        var trimmed = text.Trim();

        return FindAsync(
            document => SearchableTexts(document)
                .Any(value => value.Contains(trimmed, StringComparison.OrdinalIgnoreCase)),
            cancellationToken);
    }

    public Task SaveAsync(IReadOnlyCollection<TAggregate> aggregates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        if (aggregates.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Mapped before the store is locked, so a mapping error writes nothing. When a batch
        // contains the same aggregate twice, the last one wins, as with two separate saves.
        var replacements = new OrderedDictionary<Guid, TDocument>();

        foreach (var aggregate in aggregates)
        {
            replacements[aggregate.Id] = ToDocument(aggregate);
        }

        return _store.UpdateAsync(current => Merge(current, replacements), cancellationToken);
    }

    public Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return Task.CompletedTask;
        }

        var removed = ids.ToHashSet();

        return _store.UpdateAsync(
            current => [.. current.Where(document => !removed.Contains(document.Id))],
            cancellationToken);
    }

    /// <summary>
    /// Restores the aggregates whose documents match the predicate, in stored order. Filtering
    /// on documents means only the matching aggregates are restored.
    /// </summary>
    protected async Task<IReadOnlyList<TAggregate>> FindAsync(
        Func<TDocument, bool> predicate,
        CancellationToken cancellationToken)
    {
        var documents = await _store.LoadAsync(cancellationToken);

        return [.. documents.Where(predicate).Select(Restore)];
    }

    protected abstract TDocument ToDocument(TAggregate aggregate);

    /// <summary>
    /// Rebuilds the aggregate through its <c>Restore</c> factory, so the domain rules also
    /// check stored data.
    /// </summary>
    protected abstract TAggregate ToAggregate(TDocument document);

    /// <summary>
    /// The texts that <see cref="SearchAsync"/> looks in: the name and the description.
    /// </summary>
    protected abstract IEnumerable<string> SearchableTexts(TDocument document);

    private static List<TDocument> Merge(
        IReadOnlyList<TDocument> current,
        OrderedDictionary<Guid, TDocument> replacements)
    {
        var pending = new OrderedDictionary<Guid, TDocument>(replacements);
        var merged = new List<TDocument>(current.Count + pending.Count);

        foreach (var document in current)
        {
            merged.Add(pending.Remove(document.Id, out var replacement) ? replacement : document);
        }

        merged.AddRange(pending.Values);

        return merged;
    }

    private TAggregate Restore(TDocument document)
    {
        try
        {
            return ToAggregate(document);
        }
        catch (DomainException exception)
        {
            throw new InvalidDataException(
                $"The stored {typeof(TAggregate).Name.ToLowerInvariant()} {document.Id} in '{_store.FilePath}' "
                + $"is invalid: {exception.Message}",
                exception);
        }
    }
}
