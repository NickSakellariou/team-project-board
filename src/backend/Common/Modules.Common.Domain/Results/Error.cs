namespace Modules.Common.Domain.Results;

/// <summary>
/// A single failure, described as data rather than thrown as an exception.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="Error"/> carries a machine-readable <see cref="Code"/> (which the API
/// returns so a client can branch on it), a human-readable <see cref="Description"/>, and
/// a <see cref="Type"/> the API layer maps to an HTTP status code.
/// </para>
/// <para>
/// It is a <c>readonly record struct</c>: a value type, so creating one allocates nothing
/// on the heap, and immutable, so an error cannot be altered after the fact.
/// </para>
/// <para>
/// Errors are never constructed directly. Each module declares its own errors in one
/// static class (see <c>UserErrors</c>) using the factory methods below, so the full set
/// of failures a module can produce is readable in one file.
/// </para>
/// </remarks>
public readonly record struct Error
{
    private Error(string code, string description, ErrorType type)
    {
        Code = code;
        Description = description;
        Type = type;
    }

    private Error(string code, string description, int numericType)
    {
        Code = code;
        Description = description;
        NumericType = numericType;
        Type = ErrorType.Custom;
    }

    /// <summary>Gets the stable, machine-readable code, e.g. <c>Users.NotFound</c>.</summary>
    public string Code { get; }

    /// <summary>Gets the human-readable description.</summary>
    public string Description { get; }

    /// <summary>Gets the category used to derive an HTTP status code.</summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Gets the caller-defined category, meaningful only when <see cref="Type"/> is
    /// <see cref="ErrorType.Custom"/>.
    /// </summary>
    public int NumericType { get; }

    /// <summary>Creates a general failure.</summary>
    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    /// <summary>Creates an unanticipated failure.</summary>
    public static Error Unexpected(string code, string description) =>
        new(code, description, ErrorType.Unexpected);

    /// <summary>Creates a validation failure. Maps to HTTP 400.</summary>
    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    /// <summary>Creates a conflict, e.g. a duplicate. Maps to HTTP 409.</summary>
    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    /// <summary>Creates a "does not exist" failure. Maps to HTTP 404.</summary>
    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    /// <summary>Creates an authentication failure. Maps to HTTP 401.</summary>
    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    /// <summary>Creates an authorization failure. Maps to HTTP 403.</summary>
    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    /// <summary>Creates an error in a caller-defined category.</summary>
    public static Error Custom(int type, string code, string description) =>
        new(code, description, type);

    /// <summary>Determines whether this error equals <paramref name="other"/> in every field.</summary>
    public bool Equals(Error other) =>
        Type == other.Type &&
        NumericType == other.NumericType &&
        string.Equals(Code, other.Code, StringComparison.Ordinal) &&
        string.Equals(Description, other.Description, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Code, Description, Type, NumericType);
}
