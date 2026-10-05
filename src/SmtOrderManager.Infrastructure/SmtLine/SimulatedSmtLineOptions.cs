using System.ComponentModel.DataAnnotations;

namespace SmtOrderManager.Infrastructure.SmtLine;

/// <summary>
/// Settings of the simulated SMT line.
/// </summary>
public sealed class SimulatedSmtLineOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "SimulatedSmtLine";

    /// <summary>
    /// The identifier the line reports in its answers.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string LineId { get; set; } = "SMT-SIM-01";

    /// <summary>
    /// The directory accepted jobs are written to. A relative path is resolved against the
    /// current working directory. The directory is created on the first accepted job.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string InboxDirectory { get; set; } = "inbox";

    /// <summary>
    /// The schema versions of the download payload the line accepts.
    /// </summary>
    /// <remarks>
    /// Deliberately without a default: the configuration binder adds configured array values to
    /// existing ones instead of replacing them, so a default would always stay supported.
    /// </remarks>
    [MinLength(1)]
    public int[] SupportedSchemaVersions { get; set; } = [];
}
