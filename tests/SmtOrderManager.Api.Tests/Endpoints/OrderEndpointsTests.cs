using System.Net;
using System.Net.Http.Json;
using SmtOrderManager.Api.Tests.Infrastructure;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Orders;
using SmtOrderManager.Domain.Orders;

namespace SmtOrderManager.Api.Tests.Endpoints;

/// <summary>
/// Checks the HTTP mapping of the order download: acceptance and rejection are answers (200),
/// missing data is a violation (422), and an unreachable line is a failure (503).
/// </summary>
public sealed class OrderEndpointsTests : IAsyncDisposable
{
    private readonly ApiFactory _factory = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly string[] Expected = ["Board 'Controller' is too long."];

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Download_WhenLineAccepts_ReturnsAnswerAndMarksOrderDownloaded()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(Token);
        var order = await CreateOrderAsync(client);

        using var response = await PostDownloadAsync(client, order.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderDownloadResult>(ApiJson.Options, Token);
        Assert.True(result!.Accepted);
        Assert.Equal(FakeSmtLine.JobReference, result.JobReference);
        var orders = await client.GetStringAsync("/api/orders", Token);
        Assert.Contains("\"status\":\"downloaded\"", orders);
    }

    [Fact]
    public async Task Download_WhenLineRejects_Returns200WithReasonsAndKeepsDraft()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(Token);
        var order = await CreateOrderAsync(client);
        _factory.Line.RejectionReasons = ["Board 'Controller' is too long."];

        using var response = await PostDownloadAsync(client, order.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<OrderDownloadResult>(ApiJson.Options, Token);
        Assert.False(result!.Accepted);
        Assert.Equal(Expected, result.Reasons);
        var orders = await client.GetFromJsonAsync<OrderDetails[]>("/api/orders", ApiJson.Options, Token);
        Assert.Equal(OrderStatus.Draft, Assert.Single(orders!).Status);
    }

    [Fact]
    public async Task Download_WhenLineUnavailable_Returns503()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(Token);
        var order = await CreateOrderAsync(client);
        _factory.Line.IsUnavailable = true;

        using var response = await PostDownloadAsync(client, order.Id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Download_WithUnknownOrder_Returns422()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync(Token);

        using var response = await PostDownloadAsync(client, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static async Task<OrderDetails> CreateOrderAsync(HttpClient client)
    {
        var component = await ApiArrange.CreateComponentAsync(client, "RES-10K-0402", Token);
        var board = await ApiArrange.CreateBoardAsync(client, "Controller", 100m, component.Id, Token);

        return await ApiArrange.CreateOrderAsync(client, board.Id, Token);
    }

    private static Task<HttpResponseMessage> PostDownloadAsync(HttpClient client, Guid orderId) =>
        client.PostAsync($"/api/orders/{orderId}/download", content: null, Token);
}
