using Microsoft.Extensions.Hosting;

namespace SmtOrderManager.Cli.Interaction;

/// <summary>
/// Replaces the default console lifetime of the host.
/// </summary>
/// <remarks>
/// The default lifetime turns Ctrl+C into a graceful shutdown request, but the menus block in
/// <see cref="Console.ReadLine"/>, which that request cannot interrupt, so the application
/// would hang until the next Enter. Without it, Ctrl+C ends the process immediately, as usual
/// for console programs. Nothing is lost that was saved: every write is atomic, and input that
/// was not submitted yet is discarded. Quit from the main menu for a regular shutdown.
/// </remarks>
internal sealed class CliHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
