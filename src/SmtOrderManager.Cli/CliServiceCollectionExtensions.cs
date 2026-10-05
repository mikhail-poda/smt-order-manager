using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmtOrderManager.Cli.Demo;
using SmtOrderManager.Cli.Interaction;
using SmtOrderManager.Cli.Menus;

namespace SmtOrderManager.Cli;

/// <summary>
/// Registers the console user interface.
/// </summary>
internal static class CliServiceCollectionExtensions
{
    public static IServiceCollection AddCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DemoDataOptions>().Bind(configuration.GetSection(DemoDataOptions.SectionName));
        services.AddSingleton<DemoDataSeeder>();

        services.AddSingleton<IHostLifetime, CliHostLifetime>();
        services.AddSingleton(new ConsolePrompts(Console.In, Console.Out));

        services.AddSingleton<QuantityListEditor>();
        services.AddSingleton<ComponentMenu>();
        services.AddSingleton<BoardMenu>();
        services.AddSingleton<OrderMenu>();
        services.AddSingleton<MainMenu>();
        services.AddSingleton<CliApplication>();

        return services;
    }
}
