using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real API host in memory, with its own SQLite file in a temporary directory, no
/// demo data and a fake SMT line. Deletes the directory when disposed.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "smt-order-manager-tests", Guid.NewGuid().ToString("N"));

    public FakeSmtLine Line { get; } = new();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        // Pooled connections keep the database file open, which blocks deleting it on Windows.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Persistence:Provider", "Sqlite");
        builder.UseSetting("Persistence:Sqlite:DatabasePath", Path.Combine(_dataDirectory, "test.db"));
        builder.UseSetting("SimulatedSmtLine:InboxDirectory", Path.Combine(_dataDirectory, "inbox"));
        builder.UseSetting("DemoData:Enabled", "false");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmtLine>();
            services.AddSingleton<ISmtLine>(Line);
        });
    }
}
