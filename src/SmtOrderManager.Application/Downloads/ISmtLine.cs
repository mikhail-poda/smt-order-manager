namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// Port to an SMT line. The application hands over the serialized download payload; an
/// adapter in the infrastructure layer connects it to a real or simulated line.
/// </summary>
/// <remarks>
/// The port takes the JSON text, not the payload objects, so an implementation depends only on
/// the published format, exactly like a separately developed line.
/// </remarks>
public interface ISmtLine
{
    /// <summary>
    /// Sends a job to the line.
    /// </summary>
    /// <param name="payloadJson">The serialized <see cref="OrderDownloadPayload"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The line's answer. A rejection is a normal outcome, for example for an unsupported schema
    /// version or a board the line cannot handle.
    /// </returns>
    /// <exception cref="SmtLineUnavailableException">The line cannot be reached.</exception>
    Task<LineDownloadResult> DownloadAsync(string payloadJson, CancellationToken cancellationToken = default);
}
