using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmtOrderManager.Cli.Demo;
using SmtOrderManager.Cli.Interaction;
using SmtOrderManager.Cli.Menus;
using SmtOrderManager.Infrastructure.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Cli;

/// <summary>
/// The interactive console application, started by <c>Program</c> once the host is running.
/// </summary>
internal sealed partial class CliApplication(
    MainMenu mainMenu,
    DemoDataSeeder demoDataSeeder,
    ConsolePrompts prompts,
    IOptions<PersistenceOptions> persistenceOptions,
    IOptions<SqliteStorageOptions> sqliteOptions,
    IOptions<JsonStorageOptions> jsonOptions,
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
        var storage = DescribeStorage();
        var inboxDirectory = Path.GetFullPath(line.InboxDirectory);

        LogStarted(logger, storage, line.LineId);

        prompts.WriteLine("SMT Order Manager");
        prompts.WriteLine($"Storage:        {storage}");
        prompts.WriteLine($"SMT line:       {line.LineId} (inbox: {inboxDirectory})");

        if (await demoDataSeeder.SeedIfEmptyAsync(cancellationToken))
        {
            prompts.WriteLine("The store was empty, so demo data was created: 2 components, 3 boards, 2 orders.");
        }

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

    private string DescribeStorage() => persistenceOptions.Value.Provider switch
    {
        PersistenceProvider.Sqlite => $"SQLite database {Path.GetFullPath(sqliteOptions.Value.DatabasePath)}",
        PersistenceProvider.Json => $"JSON files in {Path.GetFullPath(jsonOptions.Value.DataDirectory)}",
        var other => other.ToString(),
    };

    [LoggerMessage(Level = LogLevel.Information, Message = "Started with storage {Storage} and SMT line {LineId}")]
    private static partial void LogStarted(ILogger logger, string storage, string lineId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stopped by the user")]
    private static partial void LogStopped(ILogger logger);
}
