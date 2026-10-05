using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Implements <see cref="IRepository{TAggregate}"/> on top of the SQLite database. Subclasses
/// only map between the aggregate and its rows.
/// </summary>
/// <remarks>
/// <para>
/// Every call creates its own short-lived context, so the repository can be a singleton and
/// every call is its own unit of work.
/// </para>
/// <para>
/// Saving replaces whole aggregates: the stored rows of the aggregates in the batch are deleted
/// (their child rows by cascade) and the new rows inserted, in one transaction. A batch is
/// therefore written completely or not at all, and no child row can be left over from an
/// earlier version.
/// </para>
/// </remarks>
/// <typeparam name="TAggregate">The aggregate root type.</typeparam>
/// <typeparam name="TRow">The row of the aggregate root.</typeparam>
internal abstract class SqliteRepository<TAggregate, TRow>(IDbContextFactory<SmtOrderManagerDbContext> contextFactory)
    : IRepository<TAggregate>
    where TAggregate : AggregateRoot
    where TRow : class, IRow
{
    private readonly IDbContextFactory<SmtOrderManagerDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var found = await FindAsync(rows => rows.Where(row => row.Id == id), cancellationToken);

        return found.SingleOrDefault();
    }

    public Task<IReadOnlyList<TAggregate>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var wanted = ids.ToList();

        return FindAsync(rows => rows.Where(row => wanted.Contains(row.Id)), cancellationToken);
    }

    public async Task<IReadOnlyList<TAggregate>> SearchAsync(
        string? text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return await FindAsync(rows => rows, cancellationToken);
        }

        // SQLite's LIKE ignores case for ASCII letters only. The rows are therefore filtered here,
        // with the same comparison as the JSON repositories. At this data size that is cheap.
        var trimmed = text.Trim();
        var rows = await LoadRowsAsync(all => all, cancellationToken);

        return [.. rows.Where(row => Matches(row, trimmed)).Select(Restore)];
    }

    public async Task SaveAsync(IReadOnlyCollection<TAggregate> aggregates, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aggregates);

        if (aggregates.Count == 0)
        {
            return;
        }

        // Mapped before the database is opened, so a mapping error writes nothing. When a batch
        // contains the same aggregate twice, the last one wins, as with two separate saves.
        var rows = new Dictionary<Guid, TRow>();

        foreach (var aggregate in aggregates)
        {
            rows[aggregate.Id] = ToRow(aggregate);
        }

        var ids = rows.Keys.ToList();

        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Set<TRow>().Where(row => ids.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
        db.Set<TRow>().AddRange(rows.Values);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RemoveAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return;
        }

        var removed = ids.ToList();

        // One statement, so the batch is removed atomically. Child rows go by cascade.
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Set<TRow>().Where(row => removed.Contains(row.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Restores the aggregates whose rows the filter selects. Only the selected rows are restored.
    /// </summary>
    protected async Task<IReadOnlyList<TAggregate>> FindAsync(
        Func<IQueryable<TRow>, IQueryable<TRow>> filter,
        CancellationToken cancellationToken)
    {
        var rows = await LoadRowsAsync(filter, cancellationToken);

        return [.. rows.Select(Restore)];
    }

    /// <summary>
    /// Adds the child rows that belong to the aggregate to the query, if there are any.
    /// </summary>
    protected abstract IQueryable<TRow> IncludeChildRows(IQueryable<TRow> rows);

    protected abstract TRow ToRow(TAggregate aggregate);

    /// <summary>
    /// Rebuilds the aggregate through its <c>Restore</c> factory, so the domain rules also
    /// check stored data.
    /// </summary>
    protected abstract TAggregate ToAggregate(TRow row);

    private static bool Matches(TRow row, string text) =>
        row.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
        || row.Description.Contains(text, StringComparison.OrdinalIgnoreCase);

    private async Task<List<TRow>> LoadRowsAsync(
        Func<IQueryable<TRow>, IQueryable<TRow>> filter,
        CancellationToken cancellationToken)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await filter(IncludeChildRows(db.Set<TRow>().AsNoTracking())).ToListAsync(cancellationToken);
    }

    private TAggregate Restore(TRow row)
    {
        try
        {
            return ToAggregate(row);
        }
        catch (DomainException exception)
        {
            throw new InvalidDataException(
                $"The stored {typeof(TAggregate).Name.ToLowerInvariant()} {row.Id} in the SQLite database "
                + $"is invalid: {exception.Message}",
                exception);
        }
    }
}
