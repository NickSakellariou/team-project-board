namespace Modules.Common.Domain.Results;

/// <content>
/// Implicit conversions into <see cref="Result{TValue}"/>.
/// </content>
/// <remarks>
/// These are what make the pattern pleasant to use. Because a <typeparamref name="TValue"/>,
/// an <see cref="Error"/> and a list of errors all convert implicitly, a handler body reads
/// as ordinary code:
/// <code>
/// if (user is null)
/// {
///     return UserErrors.NotFound(userId);   // becomes a failed Result
/// }
///
/// return new UserResponse(user.Id, user.Email);   // becomes a successful Result
/// </code>
/// Without them every return would need <c>Result&lt;UserResponse&gt;.FromError(...)</c>
/// noise, and the pattern would feel like a tax.
/// </remarks>
public readonly partial record struct Result<TValue>
{
    /// <summary>Wraps a value in a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => new(value);

    /// <summary>Wraps a single error in a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => new(error);

    // Why List<Error> and Error[] rather than one IReadOnlyList<Error> overload: C# forbids
    // user-defined conversions to or from an interface type, so the operator must name a
    // concrete collection. FromErrors below is the interface-friendly way in.
    /// <summary>Wraps several errors in a failed result.</summary>
    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);

    /// <summary>Wraps several errors in a failed result.</summary>
    public static implicit operator Result<TValue>(Error[] errors) => new(errors);

    /// <summary>Creates a successful result. Named alternative to the implicit conversion.</summary>
    public static Result<TValue> FromValue(TValue value) => value;

    /// <summary>Creates a failed result. Named alternative to the implicit conversion.</summary>
    public static Result<TValue> FromError(Error error) => error;

    /// <summary>Creates a failed result from any error collection.</summary>
    public static Result<TValue> FromErrors(IReadOnlyList<Error> errors) => new(errors);
}
