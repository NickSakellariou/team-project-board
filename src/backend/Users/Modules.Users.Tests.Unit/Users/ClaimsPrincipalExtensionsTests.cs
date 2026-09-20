using System.Security.Claims;
using Modules.Users.Features.Users.Shared;

namespace Modules.Users.Tests.Unit.Users;

/// <summary>
/// Covers <see cref="ClaimsPrincipalExtensions"/>.
/// </summary>
/// <remarks>
/// Four lines of code that every authenticated endpoint depends on. When this returns
/// null the endpoints do not crash — they answer <c>Request.NotAuthenticated</c>, so a
/// break here looks to a client exactly like an expired session, on every route at once.
/// The fallback is the fragile part: it exists because ASP.NET Core's JWT handler rewrites
/// the standard <c>sub</c> claim, which is framework behaviour that can change under us.
/// </remarks>
public class ClaimsPrincipalExtensionsTests
{
    private const string UserIdClaimType = "userid";

    [Fact]
    public void GetUserId_WithOurOwnClaim_ReturnsIt()
    {
        var principal = APrincipalWith(new Claim(UserIdClaimType, "user-1"));

        Assert.Equal("user-1", principal.GetUserId());
    }

    [Fact]
    public void GetUserId_WithOnlyTheRemappedClaim_FallsBackToIt()
    {
        // What a token carries once the JWT handler has rewritten `sub` into the long
        // WS-Federation URI. Without the fallback, every request would be unauthenticated.
        var principal = APrincipalWith(new Claim(ClaimTypes.NameIdentifier, "user-1"));

        Assert.Equal("user-1", principal.GetUserId());
    }

    [Fact]
    public void GetUserId_WithBothClaims_PrefersOurOwn()
    {
        var principal = APrincipalWith(
            new Claim(ClaimTypes.NameIdentifier, "remapped-id"),
            new Claim(UserIdClaimType, "our-id"));

        // Ours is the stable one: it is minted by AuthenticationService and is not subject
        // to the handler's remapping rules.
        Assert.Equal("our-id", principal.GetUserId());
    }

    [Fact]
    public void GetUserId_WithNeitherClaim_ReturnsNull()
    {
        var principal = APrincipalWith(new Claim(ClaimTypes.Email, "nick@example.com"));

        Assert.Null(principal.GetUserId());
    }

    [Fact]
    public void GetUserId_ForAnAnonymousPrincipal_ReturnsNull()
    {
        Assert.Null(new ClaimsPrincipal().GetUserId());
    }

    [Fact]
    public void GetUserId_WithNoPrincipal_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((ClaimsPrincipal)null!).GetUserId());
    }

    private static ClaimsPrincipal APrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));
}
