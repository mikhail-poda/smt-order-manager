using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Persistence;
using SmtOrderManager.Infrastructure.Persistence.Json;
using SmtOrderManager.Infrastructure.SmtLine;

namespace SmtOrderManager.Infrastructure;

/// <summary>
/// Registers the infrastructure implementations of the application's ports.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the JSON file repositories and the simulated SMT line, and binds their options
    /// from configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The options are validated when the host starts, so a configuration error stops the
    /// application with a clear message before the first user action instead of during it.
    /// </para>
    /// <para>
    /// Everything is a singleton. Each file store must exist once per file, because its lock
    /// only serializes access through that instance. The container disposes the stores when
    /// the host shuts down.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddValidatedOptions<JsonStorageOptions>(services, configuration, JsonStorageOptions.SectionName);
        AddValidatedOptions<SimulatedSmtLineOptions>(services, configuration, SimulatedSmtLineOptions.SectionName);

        services.TryAddSingleton(TimeProvider.System);

        AddFileStore<ComponentDocument>(services, JsonComponentRepository.FileName);
        AddFileStore<BoardDocument>(services, JsonBoardRepository.FileName);
        AddFileStore<OrderDocument>(services, JsonOrderRepository.FileName);

        services.AddSingleton<IComponentRepository, JsonComponentRepository>();
        services.AddSingleton<IBoardRepository, JsonBoardRepository>();
        services.AddSingleton<IOrderRepository, JsonOrderRepository>();

        services.AddSingleton<ISmtLine>(provider => new SimulatedSmtLine(
            provider.GetRequiredService<IOptions<SimulatedSmtLineOptions>>().Value,
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    private static void AddValidatedOptions<TOptions>(
        IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddFileStore<TDocument>(IServiceCollection services, string fileName)
        where TDocument : class
    {
        services.AddSingleton(provider => new JsonFileStore<TDocument>(
            provider.GetRequiredService<IOptions<JsonStorageOptions>>().Value,
            fileName));
    }
}
