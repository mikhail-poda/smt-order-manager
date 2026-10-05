namespace SmtOrderManager.Infrastructure.Persistence;

/// <summary>
/// Selects how the aggregates are stored.
/// </summary>
public sealed class PersistenceOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// The storage technology. SQLite unless configured otherwise.
    /// </summary>
    public PersistenceProvider Provider { get; init; } = PersistenceProvider.Sqlite;
}
