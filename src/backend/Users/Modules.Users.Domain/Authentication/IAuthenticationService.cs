using Modules.Common.Domain.Results;

namespace Modules.Users.Domain.Authentication;

/// <summary>
/// Issues and renews the token pair a client authenticates with.
/// </summary>
/// <remarks>
/// <para>
/// Declared in the Domain but implemented in Infrastructure. That inversion is the point
/// of Clean Architecture: the handlers depend on this interface, which knows only about
/// "log in" and "refresh", while everything about JWT signing, key material and
/// database-backed refresh tokens stays behind it.
/// </para>
/// <para>
/// The practical payoff is testability — a handler test substitutes this interface and
/// never touches cryptography — and replaceability: moving to an external identity
/// provider changes the implementation, not a single handler.
/// </para>
/// </remarks>
public interface IAuthenticationService
{
    /// <summary>
    /// Verifies credentials and issues a new token pair.
    /// </summary>
    /// <param name="email">The email address to sign in with.</param>
    /// <param name="password">The plaintext password, verified against the stored hash.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A token pair, or an error if the credentials are wrong or the account is locked.</returns>
    Task<Result<AuthenticationTokens>> LoginAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Exchanges an expired access token and its refresh token for a fresh pair.
    /// </summary>
    /// <param name="accessToken">The expired access token. Its signature is still checked.</param>
    /// <param name="refreshToken">The refresh token issued alongside it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A new token pair, or an error if either token is invalid, used or expired.</returns>
    Task<Result<AuthenticationTokens>> RefreshAsync(string accessToken, string refreshToken, CancellationToken cancellationToken);
}
