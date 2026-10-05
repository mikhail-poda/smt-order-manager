namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// A row of the <c>Components</c> table.
/// </summary>
internal sealed class ComponentRow : IRow
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
