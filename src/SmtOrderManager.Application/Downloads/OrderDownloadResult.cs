namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// The outcome of an order download for callers of the application layer.
/// </summary>
/// <param name="OrderId">The downloaded order.</param>
/// <param name="OrderName">The name of the order.</param>
/// <param name="Accepted">Whether the line accepted the order.</param>
/// <param name="LineId">The identifier of the answering line.</param>
/// <param name="ReceivedAt">The point in time at which the line received the order.</param>
/// <param name="Reasons">Why the line rejected the order. Empty when it was accepted.</param>
/// <param name="JobReference">The line's reference to the accepted job, if it provides one.</param>
public sealed record OrderDownloadResult(
    Guid OrderId,
    string OrderName,
    bool Accepted,
    string LineId,
    DateTimeOffset ReceivedAt,
    IReadOnlyList<string> Reasons,
    string? JobReference);
