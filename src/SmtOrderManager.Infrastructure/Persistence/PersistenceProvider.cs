namespace SmtOrderManager.Infrastructure.Persistence;

/// <summary>
/// The available storage technologies. Both fulfil the same repository contract.
/// </summary>
public enum PersistenceProvider
{
    /// <summary>
    /// One SQLite database file, see <see cref="Sqlite.SqliteStorageOptions"/>.
    /// </summary>
    Sqlite,

    /// <summary>
    /// One JSON file per aggregate type, see <see cref="Json.JsonStorageOptions"/>.
    /// </summary>
    Json,
}
