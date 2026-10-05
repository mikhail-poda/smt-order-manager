namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// A row of the <c>Boards</c> table, with the rows of its bill of materials. Dimensions are in
/// millimetres.
/// </summary>
internal sealed class BoardRow : IRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Length { get; init; }

    public decimal Width { get; init; }

    public List<BomEntryRow> BillOfMaterials { get; init; } = [];
}

/// <summary>
/// A row of the <c>BomEntries</c> table. <see cref="Position"/> keeps the order of the entries,
/// which a table does not keep by itself.
/// </summary>
internal sealed class BomEntryRow
{
    public Guid BoardId { get; init; }

    public Guid ComponentId { get; init; }

    public int Position { get; init; }

    public int Quantity { get; init; }
}
