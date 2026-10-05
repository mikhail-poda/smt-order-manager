using SmtOrderManager.Application.Demo;

namespace SmtOrderManager.Api.Demo;

/// <summary>
/// Creates the demo data when the host starts, if it is enabled and the store is empty.
/// </summary>
internal sealed class DemoDataHostedService(DemoDataSeeder seeder) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => seeder.SeedIfEmptyAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
