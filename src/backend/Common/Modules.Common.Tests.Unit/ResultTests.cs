using Modules.Common.Domain.Results;

namespace Modules.Common.Tests.Unit;

/// <summary>
/// Covers <see cref="Result{TValue}"/>, the type every handler in the solution returns.
/// </summary>
/// <remarks>
/// Worth testing thoroughly despite being small: a bug here is a bug in every feature.
/// The invariant these tests protect is that a result is always exactly one of success or
/// failure — never both, never neither.
/// </remarks>
public class ResultTests
{
    [Fact]
    public void ImplicitConversion_FromValue_ProducesSuccess()
    {
        Result<string> result = "hello";

        Assert.True(result.IsSuccess);
        Assert.False(result.IsError);
        Assert.Equal("hello", result.Value);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ImplicitConversion_FromError_ProducesFailure()
    {
        Result<string> result = Error.NotFound("Test.NotFound", "not found");

        Assert.True(result.IsError);
        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.Equal("Test.NotFound", result.FirstError.Code);
    }

    [Fact]
    public void ImplicitConversion_FromErrorList_KeepsEveryError()
    {
        List<Error> errors =
        [
            Error.Validation("Test.One", "first"),
            Error.Validation("Test.Two", "second")
        ];

        Result<string> result = errors;

        Assert.Equal(2, result.Errors.Count);
        Assert.Equal("Test.One", result.FirstError.Code);
    }

    [Fact]
    public void Value_WhenResultIsError_Throws()
    {
        Result<string> result = Error.Failure("Test.Failure", "failed");

        // The guard that turns "forgot to check IsError" into an immediate failure rather
        // than a null that surfaces somewhere unrelated.
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void FirstError_WhenResultIsSuccess_Throws()
    {
        Result<string> result = "value";

        Assert.Throws<InvalidOperationException>(() => result.FirstError);
    }

    [Fact]
    public void FromErrors_WithEmptyList_Throws()
    {
        // A "failed" result with no errors would satisfy neither IsSuccess nor IsError
        // meaningfully, so it must be impossible to construct.
        Assert.Throws<ArgumentException>(() => Result<string>.FromErrors([]));
    }

    [Fact]
    public void Success_CarriesNoValue()
    {
        Result<Success> result = Result.Success;

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Errors_WithTheSameContent_AreEqual()
    {
        var first = Error.NotFound("Test.NotFound", "not found");
        var second = Error.NotFound("Test.NotFound", "not found");

        // Value equality matters because tests assert on errors by constructing the
        // expected one, rather than by reaching for a reference.
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Errors_DifferingOnlyByType_AreNotEqual()
    {
        var notFound = Error.NotFound("Same.Code", "same description");
        var conflict = Error.Conflict("Same.Code", "same description");

        Assert.NotEqual(notFound, conflict);
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Forbidden)]
    public void ErrorFactories_SetTheMatchingType(ErrorType expectedType)
    {
        var error = expectedType switch
        {
            ErrorType.Validation => Error.Validation("c", "d"),
            ErrorType.NotFound => Error.NotFound("c", "d"),
            ErrorType.Conflict => Error.Conflict("c", "d"),
            ErrorType.Unauthorized => Error.Unauthorized("c", "d"),
            ErrorType.Forbidden => Error.Forbidden("c", "d"),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedType))
        };

        Assert.Equal(expectedType, error.Type);
    }
}
