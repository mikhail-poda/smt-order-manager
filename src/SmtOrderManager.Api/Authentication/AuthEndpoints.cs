using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace SmtOrderManager.Api.Authentication;

/// <summary>
/// <c>/api/auth</c>: log in with the configured user, log out, and ask who is logged in.
/// </summary>
internal static partial class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync);
        group.MapPost("/logout", (Delegate)LogoutAsync);
        group.MapGet("/me", GetCurrentUser).RequireAuthorization();

        return group;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        HttpContext httpContext,
        IOptions<AuthOptions> options,
        ILoggerFactory loggerFactory)
    {
        var auth = options.Value;
        var logger = loggerFactory.CreateLogger(typeof(AuthEndpoints));

        // The password is always verified, so a wrong username takes as long as a wrong password.
        var passwordMatches = PasswordHashing.Verify(auth.PasswordHash, request.Password);
        var usernameMatches = string.Equals(request.Username, auth.Username, StringComparison.Ordinal);

        if (!passwordMatches || !usernameMatches)
        {
            LogLoginFailed(logger, request.Username);

            return TypedResults.Problem(
                title: "The username or password is wrong.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, auth.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        LogLoggedIn(logger, auth.Username);

        return TypedResults.Ok(new CurrentUserResponse(auth.Username));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return TypedResults.NoContent();
    }

    private static Ok<CurrentUserResponse> GetCurrentUser(ClaimsPrincipal user) =>
        TypedResults.Ok(new CurrentUserResponse(user.Identity?.Name ?? string.Empty));

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Username} logged in")]
    private static partial void LogLoggedIn(ILogger logger, string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login for user {Username}")]
    private static partial void LogLoginFailed(ILogger logger, string username);
}

/// <summary>
/// The credentials of a login.
/// </summary>
internal sealed record LoginRequest(string Username, string Password);

/// <summary>
/// The logged-in user.
/// </summary>
internal sealed record CurrentUserResponse(string Username);
