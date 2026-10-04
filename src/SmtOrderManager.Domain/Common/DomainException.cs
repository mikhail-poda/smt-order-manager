namespace SmtOrderManager.Domain.Common;

/// <summary>
/// Thrown when an operation would violate a business rule of the domain model.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
