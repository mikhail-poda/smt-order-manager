using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Infrastructure.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.Persistence.Sqlite;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Infrastructure;

/// <summary>
/// Registers the infrastructure implementations of the application's ports.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the repositories of the configured persistence provider and the simulated SMT
    /// line, and binds their options from configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Persistence:Provider</c> selects SQLite (the default) or JSON files. It is read while
    /// the services are registered, so an unknown provider stops the application before it
    /// starts. Only the options of the selected provider are validated.
    /// </para>
    /// <para>
    /// The options are validated when the host starts, so a configuration error stops the
    /// application with a clear message before the first user action instead of during it.
    /// </para>
    /// <para>
    /// Everything is a singleton. Each JSON file store must exist once per file, because its
    /// lock only serializes access through that instance. The SQLite repositories create a
    /// short-lived context per call through the context factory. With SQLite, a hosted service
    /// creates the database when the host starts.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = ReadProvider(configuration);

        services.AddOptions<PersistenceOptions>().Bind(configuration.GetSection(PersistenceOptions.SectionName));
        AddOptions<SqliteStorageOptions>(
            services,
            configuration,
            SqliteStorageOptions.SectionName,
            validate: provider == PersistenceProvider.Sqlite);
        AddOptions<JsonStorageOptions>(
            services,
            configuration,
            JsonStorageOptions.SectionName,
            validate: provider == PersistenceProvider.Json);
        AddOptions<SimulatedSmtLineOptions>(services, configuration, SimulatedSmtLineOptions.SectionName, validate: true);

        services.TryAddSingleton(TimeProvider.System);

        if (provider == PersistenceProvider.Sqlite)
        {
            AddSqliteRepositories(services);
        }
        else
        {
            AddJsonRepositories(services);
        }

        services.AddSingleton<ISmtLine>(serviceProvider => new SimulatedSmtLine(
            serviceProvider.GetRequiredService<IOptions<SimulatedSmtLineOptions>>().Value,
            serviceProvider.GetRequiredService<TimeProvider>()));

        return services;
    }

    private static PersistenceProvider ReadProvider(IConfiguration configuration)
    {
        // Binding an unknown name throws an exception that names the setting. A number binds to
        // any enum value, so undefined values are checked here.
        var section = configuration.GetSection(PersistenceOptions.SectionName);
        var provider = section.Get<PersistenceOptions>()?.Provider ?? PersistenceProvider.Sqlite;

        if (!Enum.IsDefined(provider))
        {
            throw new InvalidOperationException(
                $"The persistence provider '{provider}' at '{section.Path}:{nameof(PersistenceOptions.Provider)}' "
                + $"is not supported. Use one of: {string.Join(", ", Enum.GetNames<PersistenceProvider>())}.");
        }

        return provider;
    }

    private static void AddSqliteRepositories(IServiceCollection services)
    {
        services.AddDbContextFactory<SmtOrderManagerDbContext>((serviceProvider, options) =>
            options.UseSqlite(CreateConnectionString(
                serviceProvider.GetRequiredService<IOptions<SqliteStorageOptions>>().Value)));

        services.AddHostedService(serviceProvider => new SqliteDatabaseInitializer(
            serviceProvider.GetRequiredService<IOptions<SqliteStorageOptions>>().Value,
            serviceProvider.GetRequiredService<IDbContextFactory<SmtOrderManagerDbContext>>()));

        services.AddSingleton<IComponentRepository, SqliteComponentRepository>();
        services.AddSingleton<IBoardRepository, SqliteBoardRepository>();
        services.AddSingleton<IOrderRepository, SqliteOrderRepository>();
    }

    private static void AddJsonRepositories(IServiceCollection services)
    {
        AddFileStore<ComponentDocument>(services, JsonComponentRepository.FileName);
        AddFileStore<BoardDocument>(services, JsonBoardRepository.FileName);
        AddFileStore<OrderDocument>(services, JsonOrderRepository.FileName);

        services.AddSingleton<IComponentRepository, JsonComponentRepository>();
        services.AddSingleton<IBoardRepository, JsonBoardRepository>();
        services.AddSingleton<IOrderRepository, JsonOrderRepository>();
    }

    private static string CreateConnectionString(SqliteStorageOptions options) =>
        new SqliteConnectionStringBuilder { DataSource = options.DatabasePath }.ToString();

    private static void AddOptions<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        bool validate)
        where TOptions : class
    {
        var builder = services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName));

        if (validate)
        {
            builder.ValidateDataAnnotations().ValidateOnStart();
        }
    }

    private static void AddFileStore<TDocument>(IServiceCollection services, string fileName)
        where TDocument : class
    {
        services.AddSingleton(serviceProvider => new JsonFileStore<TDocument>(
            serviceProvider.GetRequiredService<IOptions<JsonStorageOptions>>().Value,
            fileName));
    }
}
