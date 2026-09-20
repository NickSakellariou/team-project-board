using Modules.Common.Domain;

namespace Modules.Users.Domain.Tokens;

/// <summary>
/// A long-lived, single-use token that buys a new access token.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A JWT is verified by checking its signature, with no database
/// lookup — that is what makes it fast, and also what makes it impossible to revoke. Once
/// issued, it is valid until it expires. So access tokens are given a short life (minutes),
/// and this row is what lets a client get a fresh one without asking for the password again.
/// </para>
/// <para>
/// Unlike a JWT, a refresh token <i>is</i> a database row, so it can be invalidated.
/// That is the trade: one database read per refresh, in exchange for real revocation.
/// </para>
/// <para>
/// <b>Why Used is a flag rather than a delete.</b> Each refresh consumes its token and
/// issues a new one (rotation). If a token that was already used comes back, that is
/// evidence someone is replaying a stolen token — but only if the row still exists to say
/// so. Deleting it would make a replay indistinguishable from a typo.
/// </para>
/// <para>
/// <b>Why the rules are here and not in the service that calls them.</b> When a token may
/// be redeemed is a statement about the token, not about JWTs or about HTTP. Keeping
/// <see cref="CanBeRedeemedBy"/> on the entity makes every rule testable without a
/// database, a signing key or a running server — including expiry, which is impractical to
/// reach through the API. What the server *does* about a verdict stays with the caller.
/// </para>
/// </remarks>
public sealed class RefreshToken : IAuditableEntity
{
    /// <summary>Gets or sets the token value handed to the client. The primary key.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the <c>jti</c> of the access token this was issued alongside.
    /// </summary>
    /// <remarks>
    /// Ties the pair together, so a refresh token cannot be used to renew a *different*
    /// user's access token even if an attacker holds both halves from different sessions.
    /// </remarks>
    public string JwtId { get; set; } = string.Empty;

    /// <summary>Gets or sets the owning user's id.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets when this token stops being accepted.</summary>
    public DateTime ExpiryDateUtc { get; set; }

    /// <summary>Gets or sets a value indicating whether this token has been redeemed.</summary>
    public bool Used { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this token was revoked before expiry,
    /// for example by a logout or a detected replay.
    /// </summary>
    public bool Invalidated { get; set; }

    /// <inheritdoc />
    public DateTime CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Decides whether this token may be exchanged for a new token pair.
    /// </summary>
    /// <param name="jwtId">The <c>jti</c> of the access token presented alongside it.</param>
    /// <param name="userId">The user id claimed by that access token.</param>
    /// <param name="utcNow">The current UTC time, supplied so expiry is testable.</param>
    /// <returns>
    /// <see cref="RefreshTokenRedemption.Allowed"/>, or the first rule that rejected it.
    /// </returns>
    /// <remarks>
    /// The order is deliberate. <see cref="Used"/> is checked before everything else
    /// because a replayed token has to be reported as a replay even when it is also
    /// expired or already invalidated — those are the states a replayed token is most
    /// likely to be in, and the caller's response to a replay is what protects the account.
    /// </remarks>
    public RefreshTokenRedemption CanBeRedeemedBy(string jwtId, string userId, DateTime utcNow)
    {
        if (Used)
        {
            return RefreshTokenRedemption.Replayed;
        }

        if (Invalidated)
        {
            return RefreshTokenRedemption.Invalidated;
        }

        // <= rather than <: a token expiring exactly now is spent. The boundary is worth
        // stating because the alternative gives a token one extra tick of life, which is
        // the kind of detail that only ever shows up in a flaky test.
        if (ExpiryDateUtc <= utcNow)
        {
            return RefreshTokenRedemption.Expired;
        }

        // Binds the refresh token to the exact access token it was issued with, so a
        // refresh token from one session cannot renew another session's access token.
        if (!string.Equals(JwtId, jwtId, StringComparison.Ordinal))
        {
            return RefreshTokenRedemption.WrongAccessToken;
        }

        if (!string.Equals(UserId, userId, StringComparison.Ordinal))
        {
            return RefreshTokenRedemption.WrongUser;
        }

        return RefreshTokenRedemption.Allowed;
    }

    /// <summary>
    /// Marks this token spent, so redeeming it again is detected as a replay.
    /// </summary>
    public void MarkUsed() => Used = true;
}
