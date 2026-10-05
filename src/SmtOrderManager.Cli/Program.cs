using System.Globalization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmtOrderManager.Application;
using SmtOrderManager.Cli;
using SmtOrderManager.Infrastructure;

// Logs problems that occur before the configured logger exists, such as an unreadable
// appsettings.json. It is replaced by the configured logger once the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    // The content root is the output directory, so appsettings.json is found wherever the
    // application is started from. Relative data, inbox and log paths are resolved against
    // the current working directory.
    var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services));

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration);

    builder.Services.AddSingleton<CliApplication>();

    using var host = builder.Build();

    // Starting the host validates the options, so a configuration error ends up in the catch
    // block below before the user sees a menu.
    await host.StartAsync();

    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
    var exitCode = await host.Services
        .GetRequiredService<CliApplication>()
        .RunAsync(lifetime.ApplicationStopping);

    await host.StopAsync();

    return exitCode;
}
catch (OperationCanceledException)
{
    // Ctrl+C stops the host, which cancels the running application. That is a normal exit.
    return 0;
}
catch (Exception exception)
{
    Log.Fatal(exception, "The application terminated unexpectedly");

    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
