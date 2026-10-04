namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// The answer of an SMT line to a download.
/// </summary>
/// <param name="Accepted">Whether the line accepted the job.</param>
/// <param name="LineId">The identifier of the answering line.</param>
/// <param name="ReceivedAt">The point in time at which the line received the job.</param>
/// <param name="Reasons">Why the job was rejected. Empty when it was accepted.</param>
/// <param name="JobReference">
/// The line's reference to an accepted job, for example the file in its inbox. <see langword="null"/>
/// when the job was rejected or the line has no such reference.
/// </param>
public sealed record LineDownloadResult(
    bool Accepted,
    string LineId,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<string> Reasons,
    string? JobReference = null)
{
    public static LineDownloadResult Accept(string lineId, DateTimeOffset receivedAt, string? jobReference = null) =>
        new(Accepted: true, lineId, receivedAt, [], jobReference);

    public static LineDownloadResult Reject(string lineId, DateTimeOffset receivedAt, IReadOnlyList<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(reasons);

        if (reasons.Count == 0)
        {
            throw new ArgumentException("A rejection needs at least one reason.", nameof(reasons));
        }

        return new LineDownloadResult(Accepted: false, lineId, receivedAt, reasons.ToList().AsReadOnly());
    }
}
