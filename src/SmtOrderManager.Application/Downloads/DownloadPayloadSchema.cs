namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// Version information of the order download payload.
/// </summary>
/// <remarks>
/// Within a version, the payload only changes additively: new fields may be added, existing
/// fields keep their name, type and meaning. Consumers ignore fields they do not know. Any
/// other change is breaking and requires a new version.
/// </remarks>
public static class DownloadPayloadSchema
{
    /// <summary>
    /// The schema version this application writes.
    /// </summary>
    public const int CurrentVersion = 1;
}
