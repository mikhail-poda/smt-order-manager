using SmtOrderManager.Domain.Common;

namespace SmtOrderManager.Application.Common;

/// <summary>
/// Collects the violations of a batch, so every item is checked and all problems are reported
/// together instead of stopping at the first one.
/// </summary>
internal sealed class ViolationCollector
{
    private readonly List<Violation> _violations = [];

    public bool HasViolations => _violations.Count > 0;

    public int Count => _violations.Count;

    public IReadOnlyList<Violation> Violations => _violations.AsReadOnly();

    public void Add(string target, string message) => _violations.Add(new Violation(target, message));

    /// <summary>
    /// Runs a factory and records a violated domain rule instead of throwing.
    /// </summary>
    /// <returns>The created object, or <see langword="null"/> when a domain rule was violated.</returns>
    public T? TryCreate<T>(string target, Func<T> factory)
        where T : class
    {
        try
        {
            return factory();
        }
        catch (DomainException exception)
        {
            Add(target, exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Runs a change and records a violated domain rule instead of throwing.
    /// </summary>
    /// <returns>Whether the change succeeded.</returns>
    public bool TryApply(string target, Action change)
    {
        try
        {
            change();
            return true;
        }
        catch (DomainException exception)
        {
            Add(target, exception.Message);
            return false;
        }
    }

    /// <summary>
    /// Formats the violations for a log message.
    /// </summary>
    public override string ToString() =>
        string.Join("; ", _violations.Select(violation => $"{violation.Target}: {violation.Message}"));
}
