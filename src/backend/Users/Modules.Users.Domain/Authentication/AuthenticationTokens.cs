namespace Modules.Users.Domain.Authentication;

/// <summary>
/// The pair of tokens a signed-in client holds.
/// </summary>
/// <param name="AccessToken">
/// The JWT sent on every request as <c>Authorization: Bearer &lt;token&gt;</c>. Short-lived.
/// </param>
/// <param name="RefreshToken">
/// Single-use, long-lived, and exchanged for a new pair when the access token expires.
/// </param>
/// <param name="ExpiresAtUtc">
/// When the access token stops working. Returned so the client can refresh just before
/// expiry rather than discovering it by getting a 401 mid-action.
/// </param>
/// <remarks>
/// A positional record: immutable, value-equal and one line. The obvious alternative — a
/// class with three settable properties — would allow a token pair to be half-populated.
/// </remarks>
public sealed record AuthenticationTokens(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);
