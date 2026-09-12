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
}
