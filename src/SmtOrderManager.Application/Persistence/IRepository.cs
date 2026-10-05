using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Application.Persistence;

/// <summary>
/// Loads and saves aggregates of one type. Every aggregate root has its own repository; the
/// parts of an aggregate, such as order lines or BOM entries, are always loaded and saved with
/// their root.
/// </summary>
/// <remarks>
/// <para>
/// Writes take a batch, so an implementation can store a whole batch in one atomic step:
/// either every aggregate in the batch is written or none is.
/// </para>
/// <para>
/// Loaded aggregates are independent of the stored state. Changing a loaded aggregate has no
/// effect until it is passed to <see cref="SaveAsync"/>.
/// </para>
/// <para>
/// Lists come back in no particular order. Callers that show them sort them, so every
/// implementation can return what its storage delivers most naturally.
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
public interface IRepository<TAggregate>
    where TAggregate : AggregateRoot
{
    /// <summary>
    /// Loads one aggregate.
    /// </summary>
    /// <returns>The aggregate, or <see langword="null"/> when no aggregate has this identifier.</returns>
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads several aggregates. Unknown identifiers are skipped, so callers that need every
    /// aggregate compare the result with the requested identifiers.
    /// </summary>
    Task<IReadOnlyList<TAggregate>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the aggregates whose name or description contains the text, ignoring case.
    /// </summary>
    /// <param name="text">The search text. <see langword="null"/> or blank returns all aggregates.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyList<TAggregate>> SearchAsync(string? text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a batch atomically. Aggregates with a new identifier are added; aggregates with an
    /// existing identifier replace the stored version.
    /// </summary>
    Task SaveAsync(IReadOnlyCollection<TAggregate> aggregates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a batch atomically. Unknown identifiers are ignored. Business rules for removal,
    /// such as restricted deletion, are checked by the caller beforehand.
    /// </summary>
    Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
}
