using Microsoft.AspNetCore.Http.HttpResults;
using SmtOrderManager.Application.Common;

namespace SmtOrderManager.Api.Endpoints;

/// <summary>
/// Maps the outcome of a use case to an HTTP result, in one place for all endpoints.
/// </summary>
/// <remarks>
/// A success is 200 with the value. Violated business rules are 422 with the list of
/// violations, each with its target and message, exactly as the use case reports them. An
/// empty batch is 400: the use cases treat it as a programming error, but over HTTP it is a
/// malformed request.
/// </remarks>
internal static class ApiResults
{
    public const string ViolationsKey = "violations";

    public static Results<Ok<T>, ProblemHttpResult> From<T>(OperationResult<T> result)
    {
        if (result.Succeeded)
        {
            return TypedResults.Ok(result.Value);
        }

        return Violations(result.Violations);
    }

    public static async Task<Results<Ok<T>, ProblemHttpResult>> FromBatchAsync<TItem, T>(
        IReadOnlyCollection<TItem> items,
        Func<Task<OperationResult<T>>> operation)
    {
        if (items.Count == 0)
        {
            return EmptyBatch();
        }

        return From(await operation());
    }

    public static async Task<Results<Ok<RemoveResponse>, ProblemHttpResult>> FromRemovalAsync(
        IReadOnlyCollection<Guid> ids,
        Func<Task<OperationResult<int>>> operation)
    {
        if (ids.Count == 0)
        {
            return EmptyBatch();
        }

        var result = await operation();

        if (result.Succeeded)
        {
            return TypedResults.Ok(new RemoveResponse(result.Value));
        }

        return Violations(result.Violations);
    }

    private static ProblemHttpResult Violations(IReadOnlyList<Violation> violations) =>
        TypedResults.Problem(
            title: "The request violates business rules.",
            statusCode: StatusCodes.Status422UnprocessableEntity,
            extensions: new Dictionary<string, object?> { [ViolationsKey] = violations });

    private static ProblemHttpResult EmptyBatch() =>
        TypedResults.Problem(
            title: "The request contains no items.",
            detail: "Send at least one item.",
            statusCode: StatusCodes.Status400BadRequest);
}

/// <summary>
/// The answer to a successful removal.
/// </summary>
/// <param name="Removed">The number of removed items.</param>
internal sealed record RemoveResponse(int Removed);
