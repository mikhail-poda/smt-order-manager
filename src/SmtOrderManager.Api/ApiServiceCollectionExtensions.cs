using System.Text.Json;
using System.Text.Json.Serialization;
using SmtOrderManager.Api.Demo;
using SmtOrderManager.Api.ErrorHandling;

namespace SmtOrderManager.Api;

/// <summary>
/// Registers the services of the Web API host.
/// </summary>
internal static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers problem details, error mapping, JSON settings, OpenAPI, the health check and
    /// demo data seeding at startup.
    /// </summary>
    /// <remarks>
    /// Call it after <c>AddInfrastructure</c>: hosted services start in registration order, and
    /// the demo data needs the database the infrastructure creates.
    /// </remarks>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<SmtLineUnavailableExceptionHandler>();

        services.ConfigureHttpJsonOptions(options =>
        {
            // Enums as camelCase names, for example "draft". Requests must contain every
            // property, and null only where the command allows it, so an incomplete request is
            // rejected with 400 before it reaches a use case.
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.SerializerOptions.RespectNullableAnnotations = true;
            options.SerializerOptions.RespectRequiredConstructorParameters = true;
        });

        services.AddOpenApi();
        services.AddHealthChecks();
        services.AddHostedService<DemoDataHostedService>();

        return services;
    }
}
