using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmtOrderManager.Application.Downloads;

namespace SmtOrderManager.Api.ErrorHandling;

/// <summary>
/// Answers an unreachable SMT line with 503 Service Unavailable. The download use case has
/// already logged the failure. Every other exception falls through to the default handler,
/// which logs it and answers 500.
/// </summary>
internal sealed class SmtLineUnavailableExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not SmtLineUnavailableException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The SMT line is unavailable.",
                Detail = exception.Message,
            },
        });
    }
}
