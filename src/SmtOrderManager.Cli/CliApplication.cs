using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Cli;

/// <summary>
/// The interactive console application, started by <c>Program</c> once the host is running.
/// </summary>
internal sealed partial class CliApplication(
    IOptions<JsonStorageOptions> storageOptions,
    IOptions<SimulatedSmtLineOptions> lineOptions,
    ILogger<CliApplication> logger)
{
    /// <summary>
    /// Runs the application until the user quits or the host stops.
    /// </summary>
    /// <returns>The process exit code.</returns>
    public Task<int> RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var line = lineOptions.Value;
        var dataDirectory = Path.GetFullPath(storageOptions.Value.DataDirectory);
        var inboxDirectory = Path.GetFullPath(line.InboxDirectory);

        LogStarted(logger, dataDirectory, line.LineId);

        Console.WriteLine("SMT Order Manager");
        Console.WriteLine($"Data directory: {dataDirectory}");
        Console.WriteLine($"SMT line:       {line.LineId} (inbox: {inboxDirectory})");

        return Task.FromResult(0);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Started with data directory {DataDirectory} and SMT line {LineId}")]
    private static partial void LogStarted(ILogger logger, string dataDirectory, string lineId);
}