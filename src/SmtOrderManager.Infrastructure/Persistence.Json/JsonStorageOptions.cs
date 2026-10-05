using System.ComponentModel.DataAnnotations;

namespace SmtOrderManager.Infrastructure.Persistence.Json;

/// <summary>
/// Settings of the JSON file persistence.
/// </summary>
public sealed class JsonStorageOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "JsonStorage";

    /// <summary>
    /// The directory that holds one data file per aggregate type. A relative path is resolved
    /// against the current working directory. The directory is created on the first write.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string DataDirectory { get; set; } = "data";
}
