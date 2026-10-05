using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtOrderManager.Cli.Interaction;
using SmtOrderManager.Cli.Menus;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Cli;

/// <summary>
/// The interactive console application, started by <c>Program</c> once the host is running.
/// </summary>
internal sealed partial class CliApplication(
    MainMenu mainMenu,
    ConsolePrompts prompts,
    IOptions<JsonStorageOptions> storageOptions,
    IOptions<SimulatedSmtLineOptions> lineOptions,
    ILogger<CliApplication> logger)
{
    /// <summary>
    /// Runs the application until the user quits or the console input ends.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var line = lineOptions.Value;
        var dataDirectory = Path.GetFullPath(storageOptions.Value.DataDirectory);
        var inboxDirectory = Path.GetFullPath(line.InboxDirectory);

        LogStarted(logger, dataDirectory, line.LineId);

        prompts.WriteLine("SMT Order Manager");
        prompts.WriteLine($"Data directory: {dataDirectory}");
        prompts.WriteLine($"SMT line:       {line.LineId} (inbox: {inboxDirectory})");

        try
        {
            await mainMenu.RunAsync(cancellationToken);
        }
        catch (InputEndedException)
        {
            prompts.WriteLine();
        }

        LogStopped(logger);

        return 0;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started with data directory {DataDirectory} and SMT line {LineId}")]
    private static partial void LogStarted(ILogger logger, string dataDirectory, string lineId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped by the user")]
    private static partial void LogStopped(ILogger logger);
}