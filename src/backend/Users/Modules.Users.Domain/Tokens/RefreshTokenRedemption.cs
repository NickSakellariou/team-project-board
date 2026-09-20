namespace Modules.Users.Domain.Tokens;

/// <summary>
/// The verdict on whether a <see cref="RefreshToken"/> may be exchanged for a new token pair.
/// </summary>
/// <remarks>
/// Every rejection reaches the client as the same <c>Users.InvalidToken</c> error — telling
/// a caller <i>why</i> their token failed hands an attacker a diagnostic tool. The
/// distinction exists for the server: <see cref="Replayed"/> triggers a response the other
/// rejections do not, and the log needs to say which rule fired.
/// </remarks>
public enum RefreshTokenRedemption
{
    /// <summary>Every rule passed. The token may be redeemed.</summary>
    Allowed,

    /// <summary>
    /// The token was already spent. Either a buggy client or a stolen token being replayed.
    /// </summary>
    Replayed,

    /// <summary>The token was revoked before expiry, by a logout or a detected replay.</summary>
    Invalidated,

    /// <summary>The token is past its expiry.</summary>
    Expired,

    /// <summary>The token was issued alongside a different access token.</summary>
    WrongAccessToken,

    /// <summary>The token belongs to a different user than the one presenting it.</summary>
    WrongUser,
}
