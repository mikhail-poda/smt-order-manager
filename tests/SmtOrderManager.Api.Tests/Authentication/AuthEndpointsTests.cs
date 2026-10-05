using System.Net;
using System.Net.Http.Json;
using SmtOrderManager.Api.Authentication;
using SmtOrderManager.Api.Tests.Infrastructure;

namespace SmtOrderManager.Api.Tests.Authentication;

/// <summary>
/// Checks the cookie login: the API requires it, wrong credentials are refused, and logging out
/// ends the session.
/// </summary>
public sealed class AuthEndpointsTests : IAsyncDisposable
{
    private readonly ApiFactory _factory = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Theory]
    [InlineData("/api/components")]
    [InlineData("/api/boards")]
    [InlineData("/api/orders")]
    [InlineData("/api/auth/me")]
    public async Task Get_WithoutLogin_Returns401(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUserAndOpensApi()
    {
        using var client = _factory.CreateClient();

        using var response = await LoginAsync(client, ApiFactory.Username, ApiFactory.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me", ApiJson.Options, Token);
        Assert.Equal(ApiFactory.Username, user!.Username);
        using var components = await client.GetAsync("/api/components", Token);
        Assert.Equal(HttpStatusCode.OK, components.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsHttpOnlyStrictCookie()
    {
        using var client = _factory.CreateClient();

        using var response = await LoginAsync(client, ApiFactory.Username, ApiFactory.Password);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("SmtOrderManager.Auth=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ApiFactory.Username, "wrong-password")]
    [InlineData("someone-else", ApiFactory.Password)]
    [InlineData("TESTER", ApiFactory.Password)]
    public async Task Login_WithWrongCredentials_Returns401AndKeepsApiClosed(string username, string password)
    {
        using var client = _factory.CreateClient();

        using var response = await LoginAsync(client, username, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var components = await client.GetAsync("/api/components", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, components.StatusCode);
    }

    [Fact]
    public async Task Logout_AfterLogin_ClosesApi()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(Token);

        using var response = await client.PostAsync("/api/auth/logout", content: null, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var components = await client.GetAsync("/api/components", Token);
        Assert.Equal(HttpStatusCode.Unauthorized, components.StatusCode);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password), ApiJson.Options, Token);
}
