using System.ComponentModel.DataAnnotations;

namespace SmtOrderManager.Infrastructure.Persistence.Sqlite;

/// <summary>
/// Settings of the SQLite persistence.
/// </summary>
public sealed class SqliteStorageOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "Persistence:Sqlite";

    /// <summary>
    /// The database file. A relative path is resolved against the current working directory.
    /// The directory and the file are created at startup when they are missing.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DatabasePath { get; init; } = "data/smt-order-manager.db";
}
