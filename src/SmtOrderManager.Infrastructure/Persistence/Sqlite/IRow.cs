namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// The table row of an aggregate root. Every aggregate root has a name and a description,
/// which is what the repositories search in.
/// </summary>
internal interface IRow
{
    Guid Id { get; }

    string Name { get; }

    string Description { get; }
}
