using System.Net;
using SmtOrderManager.Api.Tests.Infrastructure;

namespace SmtOrderManager.Api.Tests.Endpoints;

/// <summary>
/// Checks the endpoints of the host itself: health check and OpenAPI document.
/// </summary>
public sealed class HostEndpointsTests : IAsyncDisposable
{
    private readonly ApiFactory _factory = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Theory]
    [InlineData("/health")]
    [InlineData("/openapi/v1.json")]
    public async Task Get_ReturnsOk(string path)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(path, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
