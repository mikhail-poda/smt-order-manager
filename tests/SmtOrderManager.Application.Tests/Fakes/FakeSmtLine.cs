using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Application.Tests.Fakes;

/// <summary>
/// Test double for <see cref="ISmtLine"/>. Records every payload it receives and answers as
/// configured: accept by default, reject with <see cref="RejectionReasons"/>, or throw when
/// <see cref="IsUnavailable"/> is set.
/// </summary>
internal sealed class FakeSmtLine : ISmtLine
{
    public const string LineId = "LINE-1";
    public const string JobReference = "inbox/job-1.json";

    public static readonly DateTimeOffset ReceivedAt = new(2026, 10, 6, 6, 0, 5, TimeSpan.FromHours(2));

    public List<string> ReceivedPayloads { get; } = [];

    public IReadOnlyList<string>? RejectionReasons { get; set; }

    public bool IsUnavailable { get; set; }

    public Task<LineDownloadResult> DownloadAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        if (IsUnavailable)
        {
            throw new SmtLineUnavailableException("Line LINE-1 is not reachable.");
        }

        ReceivedPayloads.Add(payloadJson);

        return Task.FromResult(RejectionReasons is null
            ? LineDownloadResult.Accept(LineId, ReceivedAt, JobReference)
            : LineDownloadResult.Reject(LineId, ReceivedAt, RejectionReasons));
    }
}
