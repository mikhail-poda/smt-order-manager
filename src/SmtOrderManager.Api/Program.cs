using Serilog;
using SmtOrderManager.Api;
using SmtOrderManager.Api.Authentication;
using SmtOrderManager.Api.Endpoints;
using SmtOrderManager.Api.Hosting;
using SmtOrderManager.Application;
using SmtOrderManager.Infrastructure;

// Two command-line modes that do not start the server. hash-password creates the value for
// Auth:PasswordHash; health-check is the Docker health check of a running container.
if (args is ["hash-password", var password])
{
    Console.WriteLine(PasswordHashing.Hash(password));
    return 0;
}

if (args is ["health-check"])
{
    return await HealthProbe.RunAsync();
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// The order matters for the hosted services: the infrastructure creates the database before
// the API seeds the demo data into it.
builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The web UI, built into wwwroot. Served before authentication, so the login page loads
// without a session. The UI has no client-side routes, so no fallback to index.html is needed.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapAuthEndpoints();

// Everything else under /api requires a logged-in user.
app.MapComponentEndpoints().RequireAuthorization();
app.MapBoardEndpoints().RequireAuthorization();
app.MapOrderEndpoints().RequireAuthorization();

await app.RunAsync();

return 0;
