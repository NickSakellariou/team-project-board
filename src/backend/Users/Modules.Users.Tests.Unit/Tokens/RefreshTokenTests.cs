using Modules.Users.Domain.Tokens;

namespace Modules.Users.Tests.Unit.Tokens;

/// <summary>
/// Covers <see cref="RefreshToken.CanBeRedeemedBy"/>, the rule set that decides whether a
/// refresh token still buys a new session.
/// </summary>
/// <remarks>
/// These rules were previously inside <c>AuthenticationService</c>, where reaching them
/// meant a real Postgres, a signing key and an HTTP round trip — so expiry and the two
/// binding checks had no test at all. On the entity they are a pure function, which is
/// what makes the awkward cases below cheap enough to be worth asserting.
/// </remarks>
public class RefreshTokenTests
{
    private static readonly DateTime Now = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private const string JwtId = "jwt-1";
    private const string UserId = "user-1";

    private static RefreshToken Redeemable() => new()
    {
        Token = "token-value",
        JwtId = JwtId,
        UserId = UserId,
        ExpiryDateUtc = Now.AddDays(7)
    };

    [Fact]
    public void AFreshToken_WithMatchingClaims_IsAllowed()
    {
        var token = Redeemable();

        Assert.Equal(RefreshTokenRedemption.Allowed, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void ASpentToken_IsAReplay()
    {
        var token = Redeemable();
        token.MarkUsed();

        Assert.Equal(RefreshTokenRedemption.Replayed, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void AnInvalidatedToken_IsRejected()
    {
        var token = Redeemable();
        token.Invalidated = true;

        Assert.Equal(RefreshTokenRedemption.Invalidated, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void AnExpiredToken_IsRejected()
    {
        var token = Redeemable();
        token.ExpiryDateUtc = Now.AddSeconds(-1);

        Assert.Equal(RefreshTokenRedemption.Expired, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void ATokenExpiringExactlyNow_IsRejected()
    {
        // The boundary, stated once: expiry is inclusive, so a token does not get one last
        // tick of life. Nothing else in the codebase pins this down.
        var token = Redeemable();
        token.ExpiryDateUtc = Now;

        Assert.Equal(RefreshTokenRedemption.Expired, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void ATokenExpiringOneTickFromNow_IsStillAllowed()
    {
        var token = Redeemable();
        token.ExpiryDateUtc = Now.AddTicks(1);

        Assert.Equal(RefreshTokenRedemption.Allowed, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void ATokenPairedWithADifferentAccessToken_IsRejected()
    {
        // The attack this stops: holding a valid refresh token from one session and an
        // access token from another, and splicing them together.
        var token = Redeemable();

        Assert.Equal(
            RefreshTokenRedemption.WrongAccessToken,
            token.CanBeRedeemedBy("a-different-jwt", UserId, Now));
    }

    [Fact]
    public void ATokenBelongingToAnotherUser_IsRejected()
    {
        var token = Redeemable();
        token.UserId = "someone-else";

        Assert.Equal(RefreshTokenRedemption.WrongUser, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void AReplayIsReportedAsAReplay_EvenWhenTheTokenIsAlsoExpiredAndInvalidated()
    {
        // Precedence matters: a replayed token is usually also expired or invalidated, and
        // reporting either of those instead would skip the response that protects the
        // account — cutting off the whole token chain.
        var token = Redeemable();
        token.MarkUsed();
        token.Invalidated = true;
        token.ExpiryDateUtc = Now.AddDays(-1);

        Assert.Equal(RefreshTokenRedemption.Replayed, token.CanBeRedeemedBy(JwtId, UserId, Now));
    }

    [Fact]
    public void MarkUsed_SpendsTheToken()
    {
        var token = Redeemable();

        Assert.False(token.Used);

        token.MarkUsed();

        Assert.True(token.Used);
    }
}
