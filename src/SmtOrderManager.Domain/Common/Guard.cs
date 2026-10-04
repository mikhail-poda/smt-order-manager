using System.Globalization;
using System.Runtime.CompilerServices;

namespace SmtOrderManager.Domain.Common;

/// <summary>
/// Checks values against basic domain constraints and throws a <see cref="DomainException"/>
/// when a constraint is violated. Each method returns the validated value, so it can be used
/// directly in an assignment.
/// </summary>
/// <remarks>
/// The name in the error message defaults to the expression passed by the caller. Callers can
/// pass an explicit name when the message is shown to users.
/// </remarks>
internal static class Guard
{
    /// <summary>
    /// Ensures that a text is neither null, empty nor whitespace only.
    /// </summary>
    /// <returns>The value without leading and trailing whitespace.</returns>
    public static string NotBlank(
        string? value,
        [CallerArgumentExpression(nameof(value))] string name = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"{name} must not be blank.");
        }

        return value.Trim();
    }

    /// <summary>
    /// Ensures that an identifier is not <see cref="Guid.Empty"/>.
    /// </summary>
    public static Guid NotEmpty(
        Guid value,
        [CallerArgumentExpression(nameof(value))] string name = "")
    {
        if (value == Guid.Empty)
        {
            throw new DomainException($"{name} must not be empty.");
        }

        return value;
    }

    /// <summary>
    /// Ensures that a whole number is greater than zero.
    /// </summary>
    public static int Positive(
        int value,
        [CallerArgumentExpression(nameof(value))] string name = "")
    {
        if (value <= 0)
        {
            throw new DomainException(
                string.Create(CultureInfo.InvariantCulture, $"{name} must be positive, but was {value}."));
        }

        return value;
    }

    /// <summary>
    /// Ensures that a decimal number is greater than zero.
    /// </summary>
    public static decimal Positive(
        decimal value,
        [CallerArgumentExpression(nameof(value))] string name = "")
    {
        if (value <= 0m)
        {
            throw new DomainException(
                string.Create(CultureInfo.InvariantCulture, $"{name} must be positive, but was {value}."));
        }

        return value;
    }
}
