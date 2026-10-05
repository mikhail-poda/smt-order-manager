using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SmtOrderManager.Application.Components;

namespace SmtOrderManager.Api.Endpoints;

/// <summary>
/// <c>/api/components</c>: search, create, update and remove components. Writes take a batch
/// and are all-or-nothing, like the use cases behind them.
/// </summary>
internal static class ComponentEndpoints
{
    public static RouteGroupBuilder MapComponentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/components").WithTags("Components");

        group.MapGet("/", SearchAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/", UpdateAsync);
        group.MapDelete("/", RemoveAsync);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<ComponentDetails>>> SearchAsync(
        string? search,
        ComponentService components,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await components.SearchAsync(search, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<ComponentDetails>>, ProblemHttpResult>> CreateAsync(
        [FromBody] CreateComponentCommand[] commands,
        ComponentService components,
        CancellationToken cancellationToken) =>
        ApiResults.FromBatchAsync(commands, () => components.CreateAsync(commands, cancellationToken));

    private static Task<Results<Ok<IReadOnlyList<ComponentDetails>>, ProblemHttpResult>> UpdateAsync(
        [FromBody] UpdateComponentCommand[] commands,
        ComponentService components,
        CancellationToken cancellationToken) =>
        ApiResults.FromBatchAsync(commands, () => components.UpdateAsync(commands, cancellationToken));

    private static Task<Results<Ok<RemoveResponse>, ProblemHttpResult>> RemoveAsync(
        [FromQuery(Name = "id")] Guid[] ids,
        ComponentService components,
        CancellationToken cancellationToken) =>
        ApiResults.FromRemovalAsync(ids, () => components.RemoveAsync(ids, cancellationToken));
}
