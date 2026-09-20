namespace Modules.Common.Domain.Results;

/// <summary>
/// The outcome of an operation: either success, or one or more <see cref="Error"/>s.
/// </summary>
public interface IResult
{
    /// <summary>Gets the errors. Empty when the operation succeeded.</summary>
    IReadOnlyList<Error> Errors { get; }

    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    bool IsSuccess { get; }

    /// <summary>Gets a value indicating whether the operation failed.</summary>
    bool IsError { get; }
}

/// <summary>
/// The outcome of an operation that returns a value on success.
/// </summary>
/// <typeparam name="TValue">The type returned when the operation succeeds.</typeparam>
public interface IResult<out TValue> : IResult
{
    /// <summary>Gets the value produced on success.</summary>
    TValue? Value { get; }
}
