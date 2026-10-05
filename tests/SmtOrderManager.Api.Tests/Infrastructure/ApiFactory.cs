using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmtOrderManager.Api.Authentication;
using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real API host in memory, with its own SQLite file and keys in a temporary
/// directory, a test user, no demo data and a fake SMT line. Deletes the directory when
/// disposed.
/// </summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Username = "tester";
    public const string Password = "test-password";

    // Hashing is deliberately slow, so it happens once for all tests.
    private static readonly string PasswordHash = PasswordHashing.Hash(Password);

    private readonly string _dataDirectory =
        Path.Combine(Path.GetTempPath(), "smt-order-manager-tests", Guid.NewGuid().ToString("N"));

    public FakeSmtLine Line { get; } = new();

    /// <summary>
    /// Creates a client that is logged in as the test user. The client keeps the login cookie.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();

        try
        {
            using var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest(Username, Password),
                ApiJson.Options,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

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
        builder.UseSetting("Auth:Username", Username);
        builder.UseSetting("Auth:PasswordHash", PasswordHash);
        builder.UseSetting("Auth:KeysDirectory", Path.Combine(_dataDirectory, "keys"));
        builder.UseSetting("DemoData:Enabled", "false");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmtLine>();
            services.AddSingleton<ISmtLine>(Line);
        });
    }
}
