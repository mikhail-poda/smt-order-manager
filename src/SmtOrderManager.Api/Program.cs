using Serilog;
using SmtOrderManager.Api;
using SmtOrderManager.Api.Endpoints;
using SmtOrderManager.Application;
using SmtOrderManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services));

// The order matters for the hosted services: the infrastructure creates the database before
// the API seeds the demo data into it.
builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration)
    .AddApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi();
app.MapHealthChecks("/health");

app.MapComponentEndpoints();
app.MapBoardEndpoints();
app.MapOrderEndpoints();

await app.RunAsync();
