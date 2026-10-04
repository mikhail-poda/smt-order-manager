namespace SmtOrderManager.Application.Downloads;

/// <summary>
/// Thrown by an <see cref="ISmtLine"/> when the line cannot be reached. This is a technical
/// failure, unlike a rejection, which is a normal answer of the line.
/// </summary>
public sealed class SmtLineUnavailableException : Exception
{
    public SmtLineUnavailableException()
        : base("The SMT line is unavailable.")
    {
    }

    public SmtLineUnavailableException(string message)
        : base(message)
    {
    }

    public SmtLineUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
