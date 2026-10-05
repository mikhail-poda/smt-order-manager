using System.Net.Http.Json;
using SmtOrderManager.Application.Boards;
using SmtOrderManager.Application.Components;
using SmtOrderManager.Application.Orders;

namespace SmtOrderManager.Api.Tests.Infrastructure;

/// <summary>
/// Creates test data through the API itself.
/// </summary>
internal static class ApiArrange
{
    public static readonly DateTimeOffset OrderDate = new(2026, 10, 5, 8, 30, 0, TimeSpan.FromHours(2));

    public static Task<ComponentDetails> CreateComponentAsync(
        HttpClient client,
        string name,
        CancellationToken cancellationToken) =>
        PostAsync<CreateComponentCommand, ComponentDetails>(
            client,
            "/api/components",
            new CreateComponentCommand(name, null),
            cancellationToken);

    public static Task<BoardDetails> CreateBoardAsync(
        HttpClient client,
        string name,
        decimal length,
        Guid componentId,
        CancellationToken cancellationToken) =>
        PostAsync<CreateBoardCommand, BoardDetails>(
            client,
            "/api/boards",
            new CreateBoardCommand(name, null, length, 80m, [new BomEntryInput(componentId, 12)]),
            cancellationToken);

    public static Task<OrderDetails> CreateOrderAsync(
        HttpClient client,
        Guid boardId,
        CancellationToken cancellationToken) =>
        PostAsync<CreateOrderCommand, OrderDetails>(
            client,
            "/api/orders",
            new CreateOrderCommand("Week 41", null, OrderDate, [new OrderLineInput(boardId, 500)]),
            cancellationToken);

    private static async Task<TResult> PostAsync<TCommand, TResult>(
        HttpClient client,
        string path,
        TCommand command,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(path, new[] { command }, ApiJson.Options, cancellationToken);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<TResult[]>(ApiJson.Options, cancellationToken);

        return Assert.Single(created!);
    }
}
