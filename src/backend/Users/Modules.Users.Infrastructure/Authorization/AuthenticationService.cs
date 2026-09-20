using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Modules.Common.Domain.Results;
using Modules.Common.Infrastructure.Configuration;
using Modules.Users.Domain.Authentication;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Tokens;
using Modules.Users.Domain.Users;
using Modules.Users.Infrastructure.Database;

namespace Modules.Users.Infrastructure.Authorization;

/// <summary>
/// Issues JWT access tokens and manages rotating refresh tokens.
/// </summary>
/// <remarks>
/// The one place in the solution that knows how a token is built. See
/// docs/concepts/authentication-and-jwt.md for the concepts behind it.
/// </remarks>
internal sealed class AuthenticationService(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    UsersDbContext dbContext,
    TokenValidationParameters tokenValidationParameters,
    IOptions<AuthConfiguration> authOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    /// <summary>The claim type carrying our user id, alongside the standard `sub`.</summary>
    internal const string UserIdClaimType = "userid";

    private readonly AuthConfiguration _auth = authOptions.Value;

    /// <inheritdoc />
    public async Task<Result<AuthenticationTokens>> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            // Why check the password anyway when there is no user: without it, a missing
            // account returns in microseconds and a wrong password takes the ~100ms a
            // hash verification costs. That timing difference alone reveals which emails
            // are registered. Doing the work regardless keeps the two paths comparable.
            await VerifyPasswordAgainstDummyHashAsync(password);

            logger.LogInformation("Login attempted for unknown email {Email}", email);
            return UserErrors.InvalidCredentials();
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Login attempted for locked-out user {UserId}", user.Id);
            return UserErrors.LockedOut();
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            // Records the failure and locks the account once the threshold is reached.
            // Without this call the lockout settings configured in DI do nothing —
            // CheckPasswordAsync only verifies, it does not count.
            await userManager.AccessFailedAsync(user);

            logger.LogInformation("Failed login for user {UserId}", user.Id);
            return UserErrors.InvalidCredentials();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        logger.LogInformation("User {UserId} signed in", user.Id);

        return await IssueTokensAsync(user, previousRefreshToken: null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<AuthenticationTokens>> RefreshAsync(
        string accessToken,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        // The access token is expected to be expired here — that is the whole point of
        // refreshing. Its signature must still be valid, which proves we issued it, so we
        // validate everything except the lifetime.
        var principal = GetPrincipalFromExpiredToken(accessToken);
        if (principal is null)
        {
            return UserErrors.InvalidToken();
        }

        var jwtId = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var userId = principal.FindFirstValue(UserIdClaimType);

        if (string.IsNullOrEmpty(jwtId) || string.IsNullOrEmpty(userId))
        {
            return UserErrors.InvalidToken();
        }

        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.Token == refreshToken, cancellationToken);

        var validationError = await ValidateStoredTokenAsync(storedToken, jwtId, userId, cancellationToken);
        if (validationError is not null)
        {
            return validationError.Value;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return UserErrors.InvalidToken();
        }

        return await IssueTokensAsync(user, storedToken, cancellationToken);
    }

    /// <summary>
    /// Turns the token's own verdict into an error, and performs the one reaction a
    /// verdict calls for.
    /// </summary>
    /// <remarks>
    /// The rules themselves live on <see cref="RefreshToken.CanBeRedeemedBy"/>. What is
    /// left here is what only a service can do: log, and cut off a compromised chain.
    /// </remarks>
    private async Task<Error?> ValidateStoredTokenAsync(
        RefreshToken? storedToken,
        string jwtId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (storedToken is null)
        {
            // Not a rule about a token — there is no token — so it stays out of the entity.
            logger.LogWarning("Refresh attempted with an unknown token for user {UserId}", userId);
            return UserErrors.InvalidToken();
        }

        switch (storedToken.CanBeRedeemedBy(jwtId, userId, DateTime.UtcNow))
        {
            case RefreshTokenRedemption.Allowed:
                return null;

            case RefreshTokenRedemption.Replayed:
                // A used token coming back means either a buggy client or a stolen token
                // being replayed. We cannot tell which, so we assume the worst and cut off
                // the whole chain: the legitimate user is logged out and has to sign in
                // again, which is the right trade against leaving an attacker's session alive.
                logger.LogWarning(
                    "Refresh token replay detected for user {UserId}. Invalidating all of their tokens.",
                    storedToken.UserId);

                await InvalidateAllTokensForUserAsync(storedToken.UserId, cancellationToken);

                return UserErrors.InvalidToken();

            case RefreshTokenRedemption.WrongAccessToken:
                logger.LogWarning("Refresh token does not match the supplied access token for user {UserId}", userId);
                return UserErrors.InvalidToken();

            default:
                // Invalidated, Expired and WrongUser are ordinary rejections: the client is
                // told the same thing as every other failure, and there is nothing to do.
                return UserErrors.InvalidToken();
        }
    }

    private async Task<AuthenticationTokens> IssueTokensAsync(
        User user,
        RefreshToken? previousRefreshToken,
        CancellationToken cancellationToken)
    {
        var jwtId = Guid.NewGuid().ToString();
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_auth.AccessTokenMinutes);

        var accessToken = await BuildAccessTokenAsync(user, jwtId, expiresAtUtc);

        // Rotation: the token just used is marked spent rather than deleted, so a replay
        // is detectable. See RefreshToken's remarks.
        if (previousRefreshToken is not null)
        {
            previousRefreshToken.MarkUsed();
        }

        var refreshToken = new RefreshToken
        {
            Token = GenerateSecureToken(),
            JwtId = jwtId,
            UserId = user.Id,
            ExpiryDateUtc = DateTime.UtcNow.AddDays(_auth.RefreshTokenDays)
        };

        dbContext.RefreshTokens.Add(refreshToken);

        // One SaveChanges for both the rotation and the new token, so a crash between them
        // cannot leave a user with no valid refresh token at all.
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthenticationTokens(accessToken, refreshToken.Token, expiresAtUtc);
    }

    private async Task<string> BuildAccessTokenAsync(User user, string jwtId, DateTime expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            // `sub` (subject) is the standard claim for "who this token is about".
            new(JwtRegisteredClaimNames.Sub, user.Id),
            // `jti` (JWT id) uniquely identifies this token; the refresh row points at it.
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            // Why duplicate the id in our own claim: ASP.NET Core's JWT handler remaps
            // `sub` to a long WS-Federation URI claim type by default, which makes reading
            // it from ClaimsPrincipal awkward and version-dependent. Our own name is stable.
            new(UserIdClaimType, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id)
        };

        // The user's roles go in the token so authorization needs no database read...
        var roles = await userManager.GetRolesAsync(user);
        foreach (var roleName in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));

            // ...and so do the permissions those roles carry, which is what the
            // users:read / users:update policies actually check. Flattening them into the
            // token at issue time is what makes each subsequent request a signature check
            // and nothing more.
            //
            // The cost of that speed: a permission change does not take effect until the
            // user's next access token, up to AccessTokenMinutes later.
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                continue;
            }

            var roleClaims = await roleManager.GetClaimsAsync(role);
            claims.AddRange(roleClaims);
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_auth.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _auth.Issuer,
            audience: _auth.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [SuppressMessage("Security", "CA5404:Do not disable token validation checks",
        Justification = "Intentional and confined to the refresh flow. A refresh request arrives " +
                        "precisely because the access token has expired, so the lifetime check " +
                        "would reject every legitimate refresh. Every other check — signature, " +
                        "issuer, audience, algorithm — still runs, so the token must still be one " +
                        "we issued and did not tamper with. The expiry that matters here is the " +
                        "refresh token's, which is checked against the database in " +
                        "ValidateStoredTokenAsync. These relaxed parameters are a local clone and " +
                        "never touch the ones the JWT middleware uses for normal requests.")]
    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string accessToken)
    {
        // A copy of the normal rules with lifetime checking switched off. Everything else
        // — signature, issuer, audience — still applies, so an attacker cannot simply
        // present a token they made up.
        var parameters = tokenValidationParameters.Clone();
        parameters.ValidateLifetime = false;

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(accessToken, parameters, out var validatedToken);

            // Why check the algorithm explicitly: a classic JWT attack is to re-sign a
            // token with "alg": "none" or a weaker algorithm and hope the server accepts
            // whatever the token itself declares. Pinning the expected algorithm here
            // closes that off.
            if (validatedToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch (SecurityTokenException exception)
        {
            logger.LogInformation(exception, "Rejected an access token during refresh");
            return null;
        }
    }

    // Why a cryptographic RNG rather than Guid.NewGuid(): a refresh token is a bearer
    // credential — whoever holds it can obtain access tokens. GUIDs are unique but not
    // designed to be unpredictable. 256 bits from a CSPRNG are not guessable.
    private static string GenerateSecureToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    // Marks every refresh token the user holds as invalidated. Called when a replay is
    // detected: the attacker's chain and the legitimate one are indistinguishable at this
    // point, so both are ended and the user signs in again.
    private async Task InvalidateAllTokensForUserAsync(string userId, CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && !token.Invalidated)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.Invalidated, true),
                cancellationToken);
    }

    private Task VerifyPasswordAgainstDummyHashAsync(string password)
    {
        var dummy = new User { Id = Guid.Empty.ToString(), UserName = "unknown" };
        var hash = userManager.PasswordHasher.HashPassword(dummy, "not-a-real-password");

        userManager.PasswordHasher.VerifyHashedPassword(dummy, hash, password);

        return Task.CompletedTask;
    }
}
