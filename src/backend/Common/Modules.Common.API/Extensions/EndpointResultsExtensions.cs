using Microsoft.AspNetCore.Http;
using Modules.Common.Domain.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Modules.Common.API.Extensions;

/// <summary>
/// Translates domain <see cref="Error"/>s into HTTP responses.
/// </summary>
/// <remarks>
/// This is the single seam between the domain's vocabulary of failure and HTTP's. The
/// domain never mentions status codes; this file is the only place that decides a
/// <see cref="ErrorType.NotFound"/> is a 404. Change the mapping here and every endpoint
/// in the solution follows.
/// </remarks>
public static class EndpointResultsExtensions
{
    /// <summary>
    /// Converts a single error into a problem-details response.
    /// </summary>
    /// <param name="error">The error to report.</param>
    /// <returns>A problem-details response with the status code implied by the error.</returns>
    public static IResult ToProblemResult(this Error error) => ToProblem([error]);

    /// <summary>
    /// Converts a failed result's errors into an RFC 7807 problem-details response.
    /// </summary>
    /// <param name="errors">The errors from a failed <see cref="Result{TValue}"/>.</param>
    /// <returns>A problem-details response with the status code implied by the first error.</returns>
    public static IResult ToProblem(this IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            return Results.Problem();
        }

        // Why the *first* error decides the status code: HTTP allows exactly one. When a
        // handler returns several errors they are all reported in the body, but the
        // status reflects the primary failure, which handlers put first.
        var statusCode = errors[0].Type switch
        {
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.Failure => StatusCodes.Status400BadRequest,
            ErrorType.Unexpected => StatusCodes.Status500InternalServerError,
            ErrorType.Custom => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        // The body is keyed by error Code ("Users.NotFound"), so a client can branch on a
        // stable identifier rather than string-matching the human-readable description.
        var problems = errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal);

        return Results.ValidationProblem(problems, statusCode: statusCode);
    }
}
