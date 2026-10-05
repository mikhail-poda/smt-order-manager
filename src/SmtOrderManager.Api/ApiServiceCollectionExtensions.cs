using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using SmtOrderManager.Api.Authentication;
using SmtOrderManager.Api.Demo;
using SmtOrderManager.Api.ErrorHandling;

namespace SmtOrderManager.Api;

/// <summary>
/// Registers the services of the Web API host.
/// </summary>
internal static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Registers problem details, error mapping, JSON settings, cookie login, OpenAPI, the health
    /// check and demo data seeding at startup.
    /// </summary>
    /// <remarks>
    /// Call it after <c>AddInfrastructure</c>: hosted services start in registration order, and
    /// the demo data needs the database the infrastructure creates.
    /// </remarks>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
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

        AddCookieLogin(services, configuration);

        services.AddOpenApi();
        services.AddHealthChecks();
        services.AddHostedService<DemoDataHostedService>();

        return services;
    }

    /// <summary>
    /// Cookie login for the one configured user.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cookie is <c>HttpOnly</c>, so scripts cannot read it, and <c>SameSite=Strict</c>, so
    /// the browser never sends it with a request started by another site. Together with
    /// JSON-only endpoints on the same origin as the UI, that is the protection against
    /// cross-site request forgery; there are no antiforgery tokens. It is sent over plain HTTP
    /// when the request is plain HTTP, because TLS is the job of the platform in front of the
    /// container.
    /// </para>
    /// <para>
    /// API calls without a valid cookie get 401 instead of a redirect to a login page.
    /// </para>
    /// <para>
    /// The Data Protection keys that encrypt the cookie are stored in a directory, so a restart
    /// keeps users logged in. They are stored unencrypted, which the framework logs as a warning
    /// at startup; protecting the directory is the job of the volume it lives on.
    /// </para>
    /// </remarks>
    private static void AddCookieLogin(IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => PasswordHashing.IsWellFormed(options.PasswordHash),
                "Auth:PasswordHash is not a valid password hash. Create one with: "
                + "dotnet run --project src/SmtOrderManager.Api -- hash-password <password>")
            .ValidateOnStart();

        // Needed while the services are registered; a blank value is reported by the validation.
        var keysDirectory = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()?.KeysDirectory;

        services
            .AddDataProtection()
            .SetApplicationName("SmtOrderManager")
            .PersistKeysToFileSystem(new DirectoryInfo(
                string.IsNullOrWhiteSpace(keysDirectory) ? AuthOptions.DefaultKeysDirectory : keysDirectory));

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "SmtOrderManager.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();
    }
}
