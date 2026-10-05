using Microsoft.EntityFrameworkCore;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Domain.Components;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Stores the component library in the <c>Components</c> table.
/// </summary>
internal sealed class SqliteComponentRepository(IDbContextFactory<SmtOrderManagerDbContext> contextFactory)
    : SqliteRepository<Component, ComponentRow>(contextFactory), IComponentRepository
{
    protected override IQueryable<ComponentRow> IncludeChildRows(IQueryable<ComponentRow> rows) => rows;

    protected override ComponentRow ToRow(Component aggregate) =>
        new() { Id = aggregate.Id, Name = aggregate.Name, Description = aggregate.Description };

    protected override Component ToAggregate(ComponentRow row) =>
        Component.Restore(row.Id, row.Name, row.Description);
}
