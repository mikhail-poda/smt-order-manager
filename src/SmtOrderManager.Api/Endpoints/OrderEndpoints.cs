using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SmtOrderManager.Application.Downloads;
using SmtOrderManager.Application.Orders;

namespace SmtOrderManager.Api.Endpoints;

/// <summary>
/// <c>/api/orders</c>: search, create, update and remove orders, and download an order to the
/// SMT line.
/// </summary>
/// <remarks>
/// A download the line rejects is a normal answer, so it is 200 with <c>accepted: false</c>
/// and the reasons. Only an unreachable line is an error (503).
/// </remarks>
internal static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders").WithTags("Orders");

        group.MapGet("/", SearchAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/", UpdateAsync);
        group.MapDelete("/", RemoveAsync);
        group.MapPost("/{id:guid}/download", DownloadAsync);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<OrderDetails>>> SearchAsync(
        string? search,
        OrderService orders,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await orders.SearchAsync(search, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<OrderDetails>>, ProblemHttpResult>> CreateAsync(
        [FromBody] CreateOrderCommand[] commands,
        OrderService orders,
        CancellationToken cancellationToken) =>
        ApiResults.FromBatchAsync(commands, () => orders.CreateAsync(commands, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<OrderDetails>>, ProblemHttpResult>> UpdateAsync(
        [FromBody] UpdateOrderCommand[] commands,
        OrderService orders,
        CancellationToken cancellationToken) =>
        ApiResults.FromBatchAsync(commands, () => orders.UpdateAsync(commands, cancellationToken));

    private static Task<Results<Ok<RemoveResponse>, ProblemHttpResult>> RemoveAsync(
        [FromQuery(Name = "id")] Guid[] ids,
        OrderService orders,
        CancellationToken cancellationToken) =>
        ApiResults.FromRemovalAsync(ids, () => orders.RemoveAsync(ids, cancellationToken));

    private static async Task<Results<Ok<OrderDownloadResult>, ProblemHttpResult>> DownloadAsync(
        Guid id,
        OrderDownloadService downloads,
        CancellationToken cancellationToken) =>
        ApiResults.From(await downloads.DownloadAsync(id, cancellationToken));
}
