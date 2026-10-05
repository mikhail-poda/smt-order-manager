using Microsoft.EntityFrameworkCore;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// The SQLite database: one table per aggregate root and one per list inside an aggregate.
/// </summary>
/// <remarks>
/// <para>
/// Child rows belong to their root and are deleted with it (cascade). There are deliberately no
/// foreign keys between aggregates, for example from a BOM entry to a component: aggregates
/// reference each other by identifier only, the use cases enforce restricted deletion, and the
/// JSON repositories have no such constraint either, so both providers fulfil the same contract.
/// </para>
/// <para>
/// The schema is created with <c>EnsureCreated</c>, without migrations.
/// </para>
/// </remarks>
internal sealed class SmtOrderManagerDbContext(DbContextOptions<SmtOrderManagerDbContext> options)
    : DbContext(options)
{
    public DbSet<ComponentRow> Components => Set<ComponentRow>();

    public DbSet<BoardRow> Boards => Set<BoardRow>();

    public DbSet<BomEntryRow> BomEntries => Set<BomEntryRow>();

    public DbSet<OrderRow> Orders => Set<OrderRow>();

    public DbSet<OrderLineRow> OrderLines => Set<OrderLineRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ComponentRow>(component =>
        {
            component.ToTable("Components");
            component.HasKey(row => row.Id);
        });

        modelBuilder.Entity<BoardRow>(board =>
        {
            board.ToTable("Boards");
            board.HasKey(row => row.Id);
            board.HasMany(row => row.BillOfMaterials)
                .WithOne()
                .HasForeignKey(entry => entry.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BomEntryRow>(entry =>
        {
            entry.ToTable("BomEntries");
            entry.HasKey(row => new { row.BoardId, row.ComponentId });
            entry.HasIndex(row => row.ComponentId);
        });

        modelBuilder.Entity<OrderRow>(order =>
        {
            order.ToTable("Orders");
            order.HasKey(row => row.Id);
            order.HasMany(row => row.Lines)
                .WithOne()
                .HasForeignKey(line => line.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLineRow>(line =>
        {
            line.ToTable("OrderLines");
            line.HasKey(row => new { row.OrderId, row.BoardId });
            line.HasIndex(row => row.BoardId);
        });
    }
}
