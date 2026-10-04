namespace SmtOrderManager.Application.Common;

/// <summary>
/// The outcome of a use case: either a value, or the business rules the request violated.
/// </summary>
/// <remarks>
/// Violated business rules are an expected outcome, so they are returned rather than thrown.
/// Programming errors, such as a <see langword="null"/> argument, still throw.
/// </remarks>
/// <typeparam name="T">The type of the value of a successful operation.</typeparam>
public sealed class OperationResult<T>
{
    private readonly T? _value;

    internal OperationResult(T value)
    {
        _value = value;
        Violations = [];
    }

    internal OperationResult(IEnumerable<Violation> violations)
    {
        ArgumentNullException.ThrowIfNull(violations);

        var list = violations.ToList();

        if (list.Count == 0)
        {
            throw new ArgumentException("A failed operation needs at least one violation.", nameof(violations));
        }

        Violations = list.AsReadOnly();
    }

    /// <summary>
    /// Whether the operation succeeded. Exactly then, <see cref="Violations"/> is empty.
    /// </summary>
    public bool Succeeded => Violations.Count == 0;

    /// <summary>
    /// The value of a successful operation.
    /// </summary>
    /// <exception cref="InvalidOperationException">The operation failed.</exception>
    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException("A failed operation has no value. Check Violations instead.");

    /// <summary>
    /// The violated business rules. Empty when the operation succeeded.
    /// </summary>
    public IReadOnlyList<Violation> Violations { get; }
}

/// <summary>
/// Creates <see cref="OperationResult{T}"/> instances.
/// </summary>
public static class OperationResult
{
    public static OperationResult<T> Success<T>(T value) => new(value);

    public static OperationResult<T> Failure<T>(IEnumerable<Violation> violations) => new(violations);
}
