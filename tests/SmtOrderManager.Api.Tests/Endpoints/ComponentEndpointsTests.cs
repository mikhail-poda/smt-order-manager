using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SmtOrderManager.Api.Tests.Infrastructure;
using SmtOrderManager.Application.Components;

namespace SmtOrderManager.Api.Tests.Endpoints;

/// <summary>
/// Checks the HTTP mapping of the component endpoints. The business rules behind them are
/// tested in the application tests.
/// </summary>
public sealed class ComponentEndpointsTests : IAsyncDisposable
{
    private readonly ApiFactory _factory = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Post_ThenGetWithSearch_ReturnsCreatedComponent()
    {
        using var client = _factory.CreateClient();
        var created = await ApiArrange.CreateComponentAsync(client, "RES-10K-0402", Token);
        await ApiArrange.CreateComponentAsync(client, "CAP-100N-0402", Token);

        var found = await client.GetFromJsonAsync<ComponentDetails[]>(
            "/api/components?search=res-10k",
            ApiJson.Options,
            Token);

        var component = Assert.Single(found!);
        Assert.Equal(created.Id, component.Id);
        Assert.Equal("RES-10K-0402", component.Name);
        Assert.Equal(string.Empty, component.Description);
    }

    [Fact]
    public async Task Post_WithBlankName_Returns422WithViolationsAndStoresNothing()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/components",
            new[] { new CreateComponentCommand("RES-10K-0402", null), new CreateComponentCommand("  ", null) },
            ApiJson.Options,
            Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var problem = await ReadJsonAsync(response);
        var violation = Assert.Single(problem.RootElement.GetProperty("violations").EnumerateArray());
        Assert.Equal("Item 2", violation.GetProperty("target").GetString());
        Assert.False(string.IsNullOrWhiteSpace(violation.GetProperty("message").GetString()));
        Assert.Empty((await client.GetFromJsonAsync<ComponentDetails[]>("/api/components", ApiJson.Options, Token))!);
    }

    [Fact]
    public async Task Post_WithEmptyBatch_Returns400()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/components",
            Array.Empty<CreateComponentCommand>(),
            ApiJson.Options,
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnusedComponents_ReturnsRemovedCount()
    {
        using var client = _factory.CreateClient();
        var resistor = await ApiArrange.CreateComponentAsync(client, "RES-10K-0402", Token);
        var capacitor = await ApiArrange.CreateComponentAsync(client, "CAP-100N-0402", Token);

        using var response = await client.DeleteAsync(
            $"/api/components?id={resistor.Id}&id={capacitor.Id}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJsonAsync(response);
        Assert.Equal(2, body.RootElement.GetProperty("removed").GetInt32());
    }

    [Fact]
    public async Task Delete_WithComponentUsedByBoard_Returns422AndKeepsComponent()
    {
        using var client = _factory.CreateClient();
        var resistor = await ApiArrange.CreateComponentAsync(client, "RES-10K-0402", Token);
        await ApiArrange.CreateBoardAsync(client, "Controller", 100m, resistor.Id, Token);

        using var response = await client.DeleteAsync(
            $"/api/components?id={resistor.Id}",
            Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Single((await client.GetFromJsonAsync<ComponentDetails[]>("/api/components", ApiJson.Options, Token))!);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
}
