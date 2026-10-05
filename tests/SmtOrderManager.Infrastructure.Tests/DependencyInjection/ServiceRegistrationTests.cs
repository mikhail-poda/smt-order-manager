using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmtOrderManager.Application;
using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Orders;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Infrastructure.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Infrastructure.Tests.DependencyInjection;

/// <summary>
/// Checks the registrations the hosts rely on: every use case resolves with either persistence
/// provider, and invalid configuration fails at startup.
/// </summary>
/// <remarks>
/// The CLI has no test project of its own, and this project already references Application
/// and Infrastructure, the two layers whose registrations are combined here. No test starts the
/// hosted services, so no database file is created.
/// </remarks>
public sealed class ServiceRegistrationTests
{
    private const string ProviderKey = "Persistence:Provider";

    private static readonly Dictionary<string, string?> ValidSettings = new()
    {
        ["Persistence:Sqlite:DatabasePath"] = "data/test.db",
        ["JsonStorage:DataDirectory"] = "data",
        ["SimulatedSmtLine:LineId"] = "SMT-TEST-01",
        ["SimulatedSmtLine:InboxDirectory"] = "inbox",
        ["SimulatedSmtLine:SupportedSchemaVersions:0"] = "1",
        ["SimulatedSmtLine:MaxBoardLength"] = "300",
        ["SimulatedSmtLine:MaxBoardWidth"] = "200.5",
        ["SimulatedSmtLine:IsAvailable"] = "false",
    };
    private static readonly int[] Expected = [1];

    [Theory]
    [InlineData("Sqlite")]
    [InlineData("Json")]
    public void AddApplicationAndInfrastructure_ResolvesEveryUseCase(string providerName)
    {
        using var provider = BuildProvider(With(ProviderKey, providerName));

        Assert.NotNull(provider.GetRequiredService<ComponentService>());
        Assert.NotNull(provider.GetRequiredService<BoardService>());
        Assert.NotNull(provider.GetRequiredService<OrderService>());
        Assert.NotNull(provider.GetRequiredService<OrderDownloadService>());
    }

    [Fact]
    public void AddInfrastructure_WithoutProvider_RegistersSqliteRepositoriesAndDatabaseInitializer()
    {
        using var provider = BuildProvider(ValidSettings);

        Assert.IsType<SqliteComponentRepository>(provider.GetRequiredService<IComponentRepository>());
        Assert.IsType<SqliteBoardRepository>(provider.GetRequiredService<IBoardRepository>());
        Assert.IsType<SqliteOrderRepository>(provider.GetRequiredService<IOrderRepository>());
        Assert.Same(
            provider.GetRequiredService<IOrderRepository>(),
            provider.GetRequiredService<IOrderRepository>());
        Assert.Single(provider.GetServices<IHostedService>().OfType<SqliteDatabaseInitializer>());
    }

    [Fact]
    public void AddInfrastructure_WithJsonProvider_RegistersJsonRepositoriesWithoutDatabaseInitializer()
    {
        using var provider = BuildProvider(With(ProviderKey, "json"));

        Assert.IsType<JsonComponentRepository>(provider.GetRequiredService<IComponentRepository>());
        Assert.IsType<JsonBoardRepository>(provider.GetRequiredService<IBoardRepository>());
        Assert.IsType<JsonOrderRepository>(provider.GetRequiredService<IOrderRepository>());
        Assert.Same(
            provider.GetRequiredService<JsonFileStore<OrderDocument>>(),
            provider.GetRequiredService<JsonFileStore<OrderDocument>>());
        Assert.Empty(provider.GetServices<IHostedService>().OfType<SqliteDatabaseInitializer>());
    }

    [Fact]
    public void AddInfrastructure_RegistersSimulatedLineAsSingleton()
    {
        using var provider = BuildProvider(ValidSettings);

        Assert.IsType<SimulatedSmtLine>(provider.GetRequiredService<ISmtLine>());
        Assert.Same(provider.GetRequiredService<ISmtLine>(), provider.GetRequiredService<ISmtLine>());
    }

    [Theory]
    [InlineData("Postgres")]
    [InlineData("7")]
    public void AddInfrastructure_WithUnknownProvider_ThrowsNamingSetting(string value)
    {
        var configuration = BuildConfiguration(With(ProviderKey, value));

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddInfrastructure(configuration));

        Assert.Contains(ProviderKey, exception.Message);
    }

    [Fact]
    public void AddInfrastructure_BindsOptionsFromConfiguration()
    {
        using var provider = BuildProvider(ValidSettings);

        var persistence = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
        var sqlite = provider.GetRequiredService<IOptions<SqliteStorageOptions>>().Value;
        var json = provider.GetRequiredService<IOptions<JsonStorageOptions>>().Value;
        var line = provider.GetRequiredService<IOptions<SimulatedSmtLineOptions>>().Value;

        Assert.Equal(PersistenceProvider.Sqlite, persistence.Provider);
        Assert.Equal("data/test.db", sqlite.DatabasePath);
        Assert.Equal("data", json.DataDirectory);
        Assert.Equal("SMT-TEST-01", line.LineId);
        Assert.Equal("inbox", line.InboxDirectory);
        Assert.Equal(Expected, line.SupportedSchemaVersions);
        Assert.Equal(300m, line.MaxBoardLength);
        Assert.Equal(200.5m, line.MaxBoardWidth);
        Assert.False(line.IsAvailable);
    }

    [Theory]
    [InlineData("Sqlite")]
    [InlineData("Json")]
    public void ValidateOnStart_WithValidConfiguration_Passes(string providerName)
    {
        using var provider = BuildProvider(With(ProviderKey, providerName));

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Theory]
    [InlineData("Sqlite", "Persistence:Sqlite:DatabasePath", "")]
    [InlineData("Json", "JsonStorage:DataDirectory", "")]
    [InlineData("Sqlite", "SimulatedSmtLine:LineId", " ")]
    [InlineData("Sqlite", "SimulatedSmtLine:InboxDirectory", "")]
    [InlineData("Sqlite", "SimulatedSmtLine:MaxBoardLength", "0")]
    [InlineData("Sqlite", "SimulatedSmtLine:MaxBoardWidth", "-5")]
    public void ValidateOnStart_WithInvalidValue_ThrowsNamingOption(string providerName, string key, string value)
    {
        var settings = With(ProviderKey, providerName);
        settings[key] = value;
        using var provider = BuildProvider(settings);

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());

        Assert.Contains(key.Split(':')[^1], exception.Message);
    }

    [Theory]
    [InlineData("Sqlite", "JsonStorage:DataDirectory")]
    [InlineData("Json", "Persistence:Sqlite:DatabasePath")]
    public void ValidateOnStart_WithInvalidValueOfInactiveProvider_Passes(string providerName, string key)
    {
        var settings = With(ProviderKey, providerName);
        settings[key] = string.Empty;
        using var provider = BuildProvider(settings);

        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Fact]
    public void ValidateOnStart_WithoutSupportedSchemaVersions_Throws()
    {
        var settings = new Dictionary<string, string?>(ValidSettings);
        settings.Remove("SimulatedSmtLine:SupportedSchemaVersions:0");
        using var provider = BuildProvider(settings);

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    private static Dictionary<string, string?> With(string key, string value) =>
        new(ValidSettings) { [key] = value };

    private static IConfiguration BuildConfiguration(IReadOnlyDictionary<string, string?> settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

    private static ServiceProvider BuildProvider(IReadOnlyDictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication().AddInfrastructure(BuildConfiguration(settings));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }
}
