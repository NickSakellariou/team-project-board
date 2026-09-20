using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Common.API.Logging;

namespace Modules.Common.API.ErrorHandling;

/// <summary>
/// Last-resort handler for exceptions that escape a request.
/// </summary>
/// <remarks>
/// <para>
/// Expected failures never reach here — they come back as a failed
/// <c>Result&lt;T&gt;</c> and become a 4xx. Anything that lands in this class is either a
/// bug or infrastructure being down, so it is logged at Error and reported as a 500.
/// </para>
/// <para>
/// Its job is to stop the raw exception reaching the client. A stack trace in a response
/// body tells an attacker the framework versions, file paths and internal type names.
/// </para>
/// </remarks>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        logger.UnhandledException(
            exception,
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "An error occurred while processing your request.",
                Status = StatusCodes.Status500InternalServerError,

                // Why the environment check: the exception message is useful while
                // developing and an information leak in production, where it can expose
                // connection strings, file paths or SQL. The full detail is always in the
                // log; only the response body is redacted.
                Detail = environment.IsDevelopment()
                    ? exception.ToString()
                    : "See the server logs for details."
            }
        });
    }
}
