using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SmtOrderManager.Application.Boards;

namespace SmtOrderManager.Api.Endpoints;

/// <summary>
/// <c>/api/boards</c>: search, create, update and remove boards. Writes take a batch
/// and are all-or-nothing, like the use cases behind them.
/// </summary>
internal static class BoardEndpoints
{
    public static RouteGroupBuilder MapBoardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/boards").WithTags("Boards");

        group.MapGet("/", SearchAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/", UpdateAsync);
        group.MapDelete("/", RemoveAsync);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<BoardDetails>>> SearchAsync(
        string? search,
        BoardService boards,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await boards.SearchAsync(search, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<BoardDetails>>, ProblemHttpResult>> CreateAsync(
        [FromBody] CreateBoardCommand[] commands,
        BoardService boards,
        CancellationToken cancellationToken) =>
        ApiResults.FromBatchAsync(commands, () => boards.CreateAsync(commands, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<BoardDetails>>, ProblemHttpResult>> UpdateAsync(
        [FromBody] UpdateBoardCommand[] commands,
        BoardService boards,
        CancellationToken cancellationToken) =>
        ApiResults.FromBatchAsync(commands, () => boards.UpdateAsync(commands, cancellationToken));

    private static Task<Results<Ok<RemoveResponse>, ProblemHttpResult>> RemoveAsync(
        [FromQuery(Name = "id")] Guid[] ids,
        BoardService boards,
        CancellationToken cancellationToken) =>
        ApiResults.FromRemovalAsync(ids, () => boards.RemoveAsync(ids, cancellationToken));
}
