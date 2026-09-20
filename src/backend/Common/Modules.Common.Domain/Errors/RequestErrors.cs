using Modules.Common.Domain.Results;

namespace Modules.Common.Domain.Errors;

/// <summary>
/// Failures about the incoming request itself, rather than about any one module's subject.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Common because every module produces these identically: an endpoint in
/// Projects or Boards that cannot read a caller id has exactly the failure an endpoint in
/// Users has, and cannot reference <c>Modules.Users.Domain</c> to say so.
/// </para>
/// <para>
/// The bar for adding to this file is high. An error belongs here only if it would read
/// the same in a module that knows nothing about users; anything that names a concept the
/// module owns belongs in that module's own catalogue.
/// </para>
/// </remarks>
public static class RequestErrors
{
    private const string Prefix = "Request";

    /// <summary>
    /// The request is not authenticated, or its token carries no user id.
    /// </summary>
    /// <remarks>
    /// Both cases are one error deliberately. An endpoint that reached the second case has
    /// a token the framework accepted but that is missing the claim we mint ourselves,
    /// which is a malformed credential from the caller's point of view either way.
    /// </remarks>
    public static Error NotAuthenticated() =>
        Error.Unauthorized($"{Prefix}.{nameof(NotAuthenticated)}", "The request is not authenticated.");
}
