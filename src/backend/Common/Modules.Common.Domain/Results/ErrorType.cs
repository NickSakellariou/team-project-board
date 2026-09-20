namespace Modules.Common.Domain.Results;

/// <summary>
/// The category of an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// The domain deliberately does not know about HTTP. It classifies failures in its own
/// vocabulary, and a single place in the API layer
/// (<c>EndpointResultsExtensions.ToProblem</c>) translates each category to a status code.
/// That way a handler can be reused behind a SignalR hub or a background job without
/// dragging status codes along with it.
/// </remarks>
public enum ErrorType
{
    /// <summary>An operation failed for a reason that does not fit another category.</summary>
    Failure,

    /// <summary>Something happened that the code did not anticipate.</summary>
    Unexpected,

    /// <summary>The input was not acceptable.</summary>
    Validation,

    /// <summary>The request conflicts with the current state, e.g. a duplicate.</summary>
    Conflict,

    /// <summary>The requested thing does not exist.</summary>
    NotFound,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not allowed to do this.</summary>
    Forbidden,

    /// <summary>A caller-defined category, carried in <see cref="Error.NumericType"/>.</summary>
    Custom,
}
