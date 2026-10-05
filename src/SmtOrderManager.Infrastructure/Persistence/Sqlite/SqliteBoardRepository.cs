using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Boards;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Stores boards in the <c>Boards</c> table and their bills of materials in <c>BomEntries</c>.
/// </summary>
internal sealed class SqliteBoardRepository(IDbContextFactory<SmtOrderManagerDbContext> contextFactory)
    : SqliteRepository<Board, BoardRow>(contextFactory), IBoardRepository
{
    public Task<IReadOnlyList<Board>> FindBoardsUsingComponentsAsync(
        IReadOnlyCollection<Guid> componentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(componentIds);

        var wanted = componentIds.ToList();

        return FindAsync(
            rows => rows.Where(row => row.BillOfMaterials.Any(entry => wanted.Contains(entry.ComponentId))),
            cancellationToken);
    }

    protected override IQueryable<BoardRow> IncludeChildRows(IQueryable<BoardRow> rows) =>
        rows.Include(row => row.BillOfMaterials);

    protected override BoardRow ToRow(Board aggregate) =>
        new()
        {
            Id = aggregate.Id,
            Name = aggregate.Name,
            Description = aggregate.Description,
            Length = aggregate.Length,
            Width = aggregate.Width,
            BillOfMaterials =
            [
                .. aggregate.BillOfMaterials.Select((entry, position) => new BomEntryRow
                {
                    BoardId = aggregate.Id,
                    ComponentId = entry.ComponentId,
                    Position = position,
                    Quantity = entry.Quantity,
                }),
            ],
        };

    protected override Board ToAggregate(BoardRow row) =>
        Board.Restore(
            row.Id,
            row.Name,
            row.Description,
            row.Length,
            row.Width,
            row.BillOfMaterials
                .OrderBy(entry => entry.Position)
                .Select(entry => new BomEntry(entry.ComponentId, entry.Quantity)));
}
