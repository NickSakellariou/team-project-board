using System.Security.Claims;

namespace Modules.Users.Features.Users.Shared;

/// <summary>
/// Reads our own claims off the authenticated caller.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>The claim type carrying the user id. Matches what the token is built with.</summary>
    private const string UserIdClaimType = "userid";

    /// <summary>
    /// Gets the caller's user id, or <see langword="null"/> if the request is unauthenticated.
    /// </summary>
    /// <param name="principal">The caller, from <c>HttpContext.User</c>.</param>
    /// <returns>The user id, or null.</returns>
    /// <remarks>
    /// Falls back to <see cref="ClaimTypes.NameIdentifier"/> because ASP.NET Core's JWT
    /// handler rewrites the standard <c>sub</c> claim into that long WS-Federation URI by
    /// default. Reading our own <c>userid</c> claim first keeps this independent of that
    /// remapping.
    /// </remarks>
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue(UserIdClaimType)
               ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
