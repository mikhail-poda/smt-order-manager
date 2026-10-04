namespace SmtOrderManager.Application.Tests.Fakes;

/// <summary>
/// A <see cref="TimeProvider"/> that always returns the same point in time.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
