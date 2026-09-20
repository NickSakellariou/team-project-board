using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Common.API.Extensions;
using Modules.Common.Domain.Results;
using IResult = Microsoft.AspNetCore.Http.IResult;

namespace Modules.Common.Tests.Unit;

/// <summary>
/// Covers <see cref="EndpointResultsExtensions"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the single seam between the domain's vocabulary of failure and HTTP's: the only
/// place in the solution that decides a <see cref="ErrorType.NotFound"/> is a 404. Every
/// endpoint in every module inherits whatever it says, so a one-line edit here silently
/// changes the contract of the whole API.
/// </para>
/// <para>
/// The integration suite proves two of these mappings end to end (a duplicate registration
/// is a 409, an unknown user a 404). It cannot reach the rest, because no handler returns
/// an <see cref="ErrorType.Unexpected"/> or a <see cref="ErrorType.Custom"/> yet — which is
/// exactly why those branches are worth pinning down before one does.
/// </para>
/// </remarks>
public class EndpointResultsExtensionsTests
{
    [Theory]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.Failure, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.Unexpected, StatusCodes.Status500InternalServerError)]
    [InlineData(ErrorType.Custom, StatusCodes.Status400BadRequest)]
    public void ToProblem_MapsEachErrorType_ToItsStatusCode(ErrorType type, int expectedStatusCode)
    {
        var result = new[] { AnErrorOfType(type) }.ToProblem();

        Assert.Equal(expectedStatusCode, StatusCodeOf(result));
    }

    [Fact]
    public void ToProblem_WithSeveralErrors_TakesTheStatusFromTheFirst()
    {
        var result = new[]
        {
            Error.NotFound("Users.NotFound", "User was not found."),
            Error.Conflict("Users.CannotDeleteSelf", "You cannot delete your own account.")
        }.ToProblem();

        // HTTP allows exactly one status code. Handlers put the primary failure first, so
        // "first wins" is the contract — not "most severe" or "last".
        Assert.Equal(StatusCodes.Status404NotFound, StatusCodeOf(result));
    }

    [Fact]
    public void ToProblem_ReportsEveryError_NotJustTheOneThatSetTheStatus()
    {
        var result = new[]
        {
            Error.Validation("Users.InvalidEmail", "Email is required."),
            Error.Validation("Users.InvalidPassword", "Password is too short.")
        }.ToProblem();

        var errors = ProblemDetailsOf(result).Errors;

        Assert.Equal(2, errors.Count);
        Assert.Equal(["Email is required."], errors["Users.InvalidEmail"]);
        Assert.Equal(["Password is too short."], errors["Users.InvalidPassword"]);
    }

    [Fact]
    public void ToProblem_KeysTheBodyByErrorCode_NotByDescription()
    {
        var result = new[] { Error.NotFound("Users.NotFound", "User 'abc' was not found.") }.ToProblem();

        // The code is the stable identifier a client branches on. Keying by the
        // description instead would make every reworded message a breaking change.
        Assert.True(ProblemDetailsOf(result).Errors.ContainsKey("Users.NotFound"));
    }

    [Fact]
    public void ToProblem_GroupsErrorsSharingACode_IntoOneEntry()
    {
        var result = new[]
        {
            Error.Validation("Users.InvalidField", "Email is required."),
            Error.Validation("Users.InvalidField", "Password is required.")
        }.ToProblem();

        var errors = ProblemDetailsOf(result).Errors;

        // Grouping is what stops the second failure being dropped when two share a code.
        var descriptions = Assert.Single(errors).Value;
        Assert.Equal(["Email is required.", "Password is required."], descriptions);
    }

    [Fact]
    public void ToProblem_WithNoErrors_IsA500RatherThanAnEmptySuccess()
    {
        var result = Array.Empty<Error>().ToProblem();

        // Unreachable through a Result, which cannot be a failure with no errors. If it
        // ever happens the cause is a bug on the server, so it must not look like a 400
        // that blames the caller.
        Assert.Equal(StatusCodes.Status500InternalServerError, StatusCodeOf(result));
    }

    [Fact]
    public void ToProblem_WithNullErrors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyList<Error>)null!).ToProblem());
    }

    [Fact]
    public void ToProblemResult_ForASingleError_MatchesTheListOverload()
    {
        var error = Error.Unauthorized("Request.NotAuthenticated", "The request is not authenticated.");

        var single = error.ToProblemResult();
        var asList = new[] { error }.ToProblem();

        Assert.Equal(StatusCodeOf(asList), StatusCodeOf(single));
        Assert.Equal(
            ProblemDetailsOf(asList).Errors["Request.NotAuthenticated"],
            ProblemDetailsOf(single).Errors["Request.NotAuthenticated"]);
    }

    private static Error AnErrorOfType(ErrorType type) => type switch
    {
        ErrorType.Failure => Error.Failure("Test.Failure", "d"),
        ErrorType.Unexpected => Error.Unexpected("Test.Unexpected", "d"),
        ErrorType.Validation => Error.Validation("Test.Validation", "d"),
        ErrorType.Conflict => Error.Conflict("Test.Conflict", "d"),
        ErrorType.NotFound => Error.NotFound("Test.NotFound", "d"),
        ErrorType.Unauthorized => Error.Unauthorized("Test.Unauthorized", "d"),
        ErrorType.Forbidden => Error.Forbidden("Test.Forbidden", "d"),
        ErrorType.Custom => Error.Custom(42, "Test.Custom", "d"),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    // Asserted through the framework's result interfaces rather than a concrete result
    // type, so a change in which IResult implementation Results.ValidationProblem returns
    // does not break these tests.
    private static int? StatusCodeOf(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode;

    private static HttpValidationProblemDetails ProblemDetailsOf(IResult result) =>
        Assert.IsType<HttpValidationProblemDetails>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
}
