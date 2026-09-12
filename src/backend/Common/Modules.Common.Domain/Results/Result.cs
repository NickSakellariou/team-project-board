using System.Diagnostics.CodeAnalysis;

namespace Modules.Common.Domain.Results;

/// <summary>
/// Entry point for creating successful results that carry no value.
/// </summary>
public static class Result
{
    /// <summary>Gets the singleton success marker. See <see cref="Results.Success"/>.</summary>
    public static Success Success => default;
}

/// <summary>
/// The result of an operation: either a <typeparamref name="TValue"/> or a list of
/// <see cref="Error"/>s, never both and never neither.
/// </summary>
/// <typeparam name="TValue">The type returned when the operation succeeds.</typeparam>
/// <remarks>
/// <para>
/// This is the return type of every handler in the solution. It makes failure part of a
/// method's signature: a caller can see from <c>Task&lt;Result&lt;UserResponse&gt;&gt;</c>
/// that the operation can fail, which a <c>Task&lt;UserResponse&gt;</c> that throws does
/// not tell them. See docs/concepts/result-pattern.md.
/// </para>
/// <para>
/// Exceptions are still used — but only for genuinely exceptional situations (the
/// database is unreachable, a bug), which <c>GlobalExceptionHandler</c> turns into a 500.
/// "This email is already taken" is an expected outcome, not an exception.
/// </para>
/// <para>
/// Construction goes through the implicit conversions in Result.ImplicitConverters.cs, so
/// a handler writes <c>return someValue;</c> or <c>return UserErrors.NotFound(id);</c> and
/// never mentions this type by name.
/// </para>
/// </remarks>
[SuppressMessage("Style", "IDE0032:Use auto property",
    Justification = "The backing field is read directly by Value, which guards access with IsError.")]
public readonly partial record struct Result<TValue> : IResult<TValue>
{
    private readonly TValue? _value;

    private Result(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
        Errors = [];
    }

    private Result(Error error)
    {
        _value = default;
        Errors = [error];
    }

    private Result(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException(
                "Cannot create a failed Result<TValue> with no errors. Provide at least one.",
                nameof(errors));
        }

        _value = default;
        Errors = errors;
    }

    /// <inheritdoc />
    public bool IsSuccess => Errors.Count == 0;

    /// <inheritdoc />
    public bool IsError => Errors.Count > 0;

    /// <inheritdoc />
    public IReadOnlyList<Error> Errors { get; } = [];

    /// <summary>Gets the value produced on success.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue? Value =>
        IsError
            // Why: throwing here rather than returning default turns "forgot to check
            // IsError" into an immediate, obvious failure instead of a silent null that
            // surfaces somewhere unrelated.
            ? throw new InvalidOperationException(
                "Value cannot be read when the result has errors. Check IsSuccess or IsError first.")
            : _value;

    /// <summary>Gets the first error.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error FirstError =>
        IsError
            ? Errors[0]
            : throw new InvalidOperationException(
                "FirstError cannot be read when the result has no errors. Check IsError first.");
}
