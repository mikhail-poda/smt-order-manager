namespace SmtOrderManager.Cli.Demo;

/// <summary>
/// Settings of the demo data.
/// </summary>
internal sealed class DemoDataOptions
{
    /// <summary>
    /// The configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "DemoData";

    /// <summary>
    /// Whether demo data is created on a start with an empty store. Set to <see langword="false"/>
    /// to start with an empty store.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
