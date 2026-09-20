using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Modules.Users.Tests.Integration.Configuration;
using Modules.Users.Tests.Integration.Contracts;

namespace Modules.Users.Tests.Integration.Tests;

/// <summary>
/// Verifies that a forged access token cannot buy a fresh token pair.
/// </summary>
/// <remarks>
/// <para>
/// The refresh endpoint is the one place that accepts an <i>expired</i> access token by
/// design, so its lifetime check is switched off and only the signature stands between a
/// caller and a new session. These tests attack that seam.
/// </para>
/// <para>
/// Why they live here rather than in a unit test: reaching
/// <c>GetPrincipalFromExpiredToken</c> directly means constructing an
/// <c>AuthenticationService</c> with a database, a UserManager and a RoleManager, and the
/// thing being proved — that the real, running application rejects the token — is then no
/// longer what was tested.
/// </para>
/// </remarks>
public class TokenSecurityTests(UsersApiFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task Refresh_WithAnAccessTokenReSignedWithADifferentAlgorithm_IsRejected()
    {
        var tokens = await RegisterAndLoginAsync();

        // HS384 rather than HS256, signed with the server's own key. The signature is
        // genuinely valid, the issuer and audience match, and the lifetime is not checked
        // on this endpoint — so every check but one passes.
        //
        // The one that stops it is the explicit algorithm comparison in
        // AuthenticationService. Without it, an attacker who found a weakness in a weaker
        // algorithm the library still accepts could pick which one to be validated under,
        // because the token itself names it. That is the classic JWT downgrade.
        var forged = ReSign(tokens.AccessToken, UsersApiFactory.SigningKey, SecurityAlgorithms.HmacSha384);

        var response = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(forged, tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnAccessTokenSignedByADifferentKey_IsRejected()
    {
        var tokens = await RegisterAndLoginAsync();

        var forged = ReSign(
            tokens.AccessToken,
            "an-attackers-own-signing-key-long-enough-for-hmac-sha256",
            SecurityAlgorithms.HmacSha256);

        var response = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(forged, tokens.RefreshToken));

        // Proves the refresh path still verifies the signature despite skipping the
        // lifetime check. Turning off ValidateIssuerSigningKey alongside ValidateLifetime
        // is a one-word mistake that would make this pass.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAnUnsignedAccessToken_IsRejected()
    {
        var tokens = await RegisterAndLoginAsync();

        // "alg": "none" — the other half of the same family of attacks.
        var original = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);
        var unsigned = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: original.Issuer,
            audience: original.Audiences.First(),
            claims: original.Claims,
            notBefore: original.ValidFrom,
            expires: original.ValidTo));

        var response = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(unsigned, tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithAForgedAccessToken_DoesNotSpendTheRefreshToken()
    {
        var tokens = await RegisterAndLoginAsync();
        var forged = ReSign(tokens.AccessToken, UsersApiFactory.SigningKey, SecurityAlgorithms.HmacSha384);

        var rejected = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(forged, tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        // The legitimate pair must still work afterwards. If a rejected attempt consumed
        // the refresh token, anyone who could send one forged request could log a user out
        // — a denial of service built out of the replay protection itself.
        var accepted = await Client.PostAsJsonAsync(
            "/api/users/refresh",
            new RefreshTokenRequest(tokens.AccessToken, tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectAnAccessTokenSignedByADifferentKey()
    {
        var tokens = await RegisterAndLoginAsync();
        var forged = ReSign(
            tokens.AccessToken,
            "an-attackers-own-signing-key-long-enough-for-hmac-sha256",
            SecurityAlgorithms.HmacSha256);

        Authenticate(forged);

        var response = await Client.GetAsync(new Uri("/api/users/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Not covered, deliberately: the JWT middleware accepts an access token re-signed with
    // HS384 or HS512, because TokenValidationParameters does not set ValidAlgorithms. Only
    // the refresh path pins HS256, in AuthenticationService. Forging either still requires
    // the signing key — and with the key an attacker would simply sign an HS256 token — so
    // this is a missing defence in depth rather than a hole. Setting
    // ValidAlgorithms = [SecurityAlgorithms.HmacSha256] would close it and make a test of
    // the middleware path worth writing.

    private async Task<TokenResponse> RegisterAndLoginAsync()
    {
        await RegisterAsync();
        return await LoginAsync();
    }

    /// <summary>
    /// Rebuilds a token with the same claims, signed with the given key and algorithm.
    /// </summary>
    private static string ReSign(string accessToken, string key, string algorithm)
    {
        var original = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

        // The signature covers the header, so re-signing under a different algorithm means
        // rebuilding the token rather than swapping bytes in the one we have.
        var claims = original.Claims
            .Where(claim => !string.Equals(claim.Type, "aud", StringComparison.Ordinal))
            .ToList();

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            algorithm);

        var token = new JwtSecurityToken(
            issuer: original.Issuer,
            audience: original.Audiences.First(),
            claims: claims,
            notBefore: original.ValidFrom,
            expires: original.ValidTo,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
