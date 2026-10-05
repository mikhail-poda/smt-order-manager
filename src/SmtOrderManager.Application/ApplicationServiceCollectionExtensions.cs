using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Orders;

namespace SmtOrderManager.Application;

/// <summary>
/// Registers the application services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the use case services. The ports they depend on, the repositories and the SMT
    /// line, are registered by the infrastructure.
    /// </summary>
    /// <remarks>
    /// The services are stateless, so they are singletons. <see cref="TimeProvider.System"/> is
    /// registered only when no other time provider is, so a host or a test can replace it.
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<ComponentService>();
        services.AddSingleton<BoardService>();
        services.AddSingleton<OrderService>();
        services.AddSingleton<OrderDownloadService>();

        return services;
    }
}
