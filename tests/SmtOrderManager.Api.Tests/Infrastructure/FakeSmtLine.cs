using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Api.Tests.Infrastructure;

/// <summary>
/// Test double for <see cref="ISmtLine"/>: accepts by default, rejects with
/// <see cref="RejectionReasons"/>, or throws when <see cref="IsUnavailable"/> is set.
/// </summary>
internal sealed class FakeSmtLine : ISmtLine
{
    public const string LineId = "LINE-TEST";
    public const string JobReference = "inbox/job-1.json";

    public static readonly DateTimeOffset ReceivedAt = new(2026, 10, 6, 6, 0, 5, TimeSpan.FromHours(2));

    public IReadOnlyList<string>? RejectionReasons { get; set; }

    public bool IsUnavailable { get; set; }

    public Task<LineDownloadResult> DownloadAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        if (IsUnavailable)
        {
            throw new SmtLineUnavailableException("Line LINE-TEST is not reachable.");
        }

        return Task.FromResult(RejectionReasons is null
            ? LineDownloadResult.Accept(LineId, ReceivedAt, JobReference)
            : LineDownloadResult.Reject(LineId, ReceivedAt, RejectionReasons));
    }
}
