namespace SmtOrderManager.Cli.Interaction;

/// <summary>
/// Thrown when the console input has ended, for example after Ctrl+Z or Ctrl+D or at the end
/// of redirected input. The application then exits normally.
/// </summary>
internal sealed class InputEndedException : Exception
{
    public InputEndedException()
        : base("The console input has ended.")
    {
    }
}
