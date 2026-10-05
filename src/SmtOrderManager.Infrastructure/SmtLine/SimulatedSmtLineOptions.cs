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
    public string LineId { get; init; } = "SMT-SIM-01";

    /// <summary>
    /// The directory accepted jobs are written to. A relative path is resolved against the
    /// current working directory. The directory is created on the first accepted job.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string InboxDirectory { get; init; } = "inbox";

    /// <summary>
    /// The schema versions of the download payload the line accepts.
    /// </summary>
    /// <remarks>
    /// Deliberately without a default: the configuration binder adds configured array values to
    /// existing ones instead of replacing them, so a default would always stay supported.
    /// </remarks>
    [MinLength(1)]
    public int[] SupportedSchemaVersions { get; init; } = [];

    /// <summary>
    /// The maximum board length in millimetres the line can handle, measured in the direction
    /// of transport.
    /// </summary>
    /// <remarks>
    /// The default is a generic value, not the limit of a specific machine.
    /// </remarks>
    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal MaxBoardLength { get; init; } = 510m;

    /// <summary>
    /// The maximum board width in millimetres the line can handle, measured across the
    /// direction of transport.
    /// </summary>
    /// <remarks>
    /// The default is a generic value, not the limit of a specific machine.
    /// </remarks>
    [Range(typeof(decimal), "0.01", "100000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal MaxBoardWidth { get; init; } = 460m;
}
